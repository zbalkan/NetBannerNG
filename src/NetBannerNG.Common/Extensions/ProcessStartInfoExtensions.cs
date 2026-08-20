using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Common.Extensions
{
    public static class ProcessStartInfoExtensions
    {
        private const uint CreateUnicodeEnvironment = 0x00000400;

        /// <summary>
        /// Get the active user's token and run the process in the given user's context
        /// </summary>
        /// <see cref="https://stackoverflow.com/questions/1109271/launching-gui-app-from-windows-service-window-does-not-appear/1109443"/>
        /// <param name="psi"> Process to be started in user context </param>
        /// <returns> Returns true if succeeds. </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool RunAsActiveUser(this ProcessStartInfo psi) =>
            psi.RunAsActiveUser(out _, out _);

        /// <summary>
        /// Get the active user's token and run the process in the given user's context.
        /// On failure, returns which step failed and the Win32 error code captured at that step.
        /// </summary>
        /// <param name="psi"> Process to be started in user context </param>
        /// <param name="failedStep">
        /// On failure, the step that failed (<c>WTSQueryUserToken</c>, <c>CreateEnvironmentBlock</c>,
        /// or <c>CreateProcessAsUser</c>). Empty string on success.
        /// </param>
        /// <param name="win32Error">On failure, the value of <c>GetLastError</c> at the failing step. Zero on success.</param>
        /// <returns> Returns true if succeeds. </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool RunAsActiveUser(this ProcessStartInfo psi, out string failedStep, out int win32Error) =>
            psi.RunAsActiveUser(out _, out failedStep, out win32Error);

        /// <summary>
        /// Get the active user's token and run the process in the given user's context,
        /// returning the PID created by <c>CreateProcessAsUser</c> on success.
        /// </summary>
        public static bool RunAsActiveUser(this ProcessStartInfo psi, out int processId, out string failedStep, out int win32Error)
        {
            var processHandle = IntPtr.Zero;
            try
            {
                return psi.RunAsActiveUser(out processId, out processHandle, out failedStep, out win32Error);
            }
            finally
            {
                if (processHandle != IntPtr.Zero)
                {
                    _ = Kernel32.CloseHandle(processHandle);
                }
            }
        }

        /// <summary>
        /// Get the active user's token and run the process in the given user context,
        /// returning the PID and original process handle created by <c>CreateProcessAsUser</c>.
        /// The caller owns and must close <paramref name="processHandle"/> on success.
        /// </summary>
        public static bool RunAsActiveUser(this ProcessStartInfo psi, out int processId, out IntPtr processHandle, out string failedStep, out int win32Error)
        {
            if (psi == null)
            {
                throw new ArgumentNullException(nameof(psi));
            }

            processId = 0;
            processHandle = IntPtr.Zero;
            failedStep = string.Empty;
            win32Error = 0;

            WindowsIdentity? user = null;
            try
            {
                if (!PrivilegeHelper.GetActiveUser(out user, out win32Error))
                {
                    failedStep = "WTSQueryUserToken";
                    return false;
                }

                return psi.RunImpersonated(user!, out processId, out processHandle, out failedStep, out win32Error);
            }
            finally
            {
                user?.Dispose();
            }
        }

        /// <summary>
        /// Run the process in the given user context
        /// </summary>
        /// <see href="https://bytes.com/topic/c-sharp/answers/463942-using-openprocesstoken"/>
        /// <param name="psi"> process to be started </param>
        /// <param name="userIdentity"> user to impersonate </param>
        /// <returns> Returns true if succeeds. </returns>
        /// <exception cref="ArgumentNullException"><paramref name="psi"/> is <c>null</c>.</exception>
        private static bool RunImpersonated(this ProcessStartInfo psi, WindowsIdentity userIdentity, out int processId, out IntPtr processHandle, out string failedStep, out int win32Error)
        {
            if (psi == null)
            {
                throw new ArgumentNullException(nameof(psi));
            }

            if (userIdentity == null)
            {
                throw new ArgumentNullException(nameof(userIdentity));
            }

            processId = 0;
            processHandle = IntPtr.Zero;
            failedStep = string.Empty;
            win32Error = 0;

            Debug.WriteLine($"Before impersonation: {WindowsIdentity.GetCurrent().Name} ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");
            var refAdded = false;
            userIdentity.AccessToken.DangerousAddRef(ref refAdded);
            var userToken = userIdentity.AccessToken.DangerousGetHandle();

            var path = psi.FileName;
            var dir = Path.GetDirectoryName(path);
            var si = new StartupInfo
            {
                lpDesktop = "winsta0\\default"
            };
            si.cb = Marshal.SizeOf(si);
            var sa = new SecurityAttributes
            {
                bInheritHandle = 0
            };
            sa.nLength = Marshal.SizeOf(sa);
            sa.lpSecurityDescriptor = IntPtr.Zero;

            Debug.WriteLine($"During impersonation: {userIdentity.Name}. ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");

            var environmentBlock = IntPtr.Zero;
            try
            {
                if (!UserEnv.CreateEnvironmentBlock(out environmentBlock, userToken, false))
                {
                    win32Error = Marshal.GetLastWin32Error();
                    failedStep = "CreateEnvironmentBlock";
                    Debug.WriteLine($"Failed to create environment block for user {userIdentity.Name}. Error code: {win32Error}");
                    return false;
                }

                var processInformation = default(ProcessInformation);
                if (!Advapi32.CreateProcessAsUser(userToken, // user token
                    path, // executable path
                    BuildCommandLine(path, psi.Arguments), // command line
                    ref sa, // process security attributes ( none )
                    ref sa, // thread  security attributes ( none )
                    false, // inherit handles?
                    CreateUnicodeEnvironment, // creation flags
                    environmentBlock, // environment variables
                    dir, // current directory of the new process
                    ref si, // startup info
                    out processInformation)) // receive process information in pi
                {
                    win32Error = Marshal.GetLastWin32Error();
                    failedStep = "CreateProcessAsUser";
                    Debug.WriteLine($"Failed to create process {psi.FileName} as user {userIdentity.Name}. Error code: {win32Error}");
                    return false;
                }

                try
                {
                    processId = processInformation.dwProcessId;
                    processHandle = processInformation.hProcess;
                    processInformation.hProcess = IntPtr.Zero;
                    Debug.WriteLine($"After impersonation: {WindowsIdentity.GetCurrent().Name} ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");
                    return true;
                }
                finally
                {
                    if (processInformation.hThread != IntPtr.Zero)
                    {
                        _ = Kernel32.CloseHandle(processInformation.hThread);
                    }

                    if (processInformation.hProcess != IntPtr.Zero)
                    {
                        _ = Kernel32.CloseHandle(processInformation.hProcess);
                    }
                }
            }
            finally
            {
                if (refAdded)
                {
                    userIdentity.AccessToken.DangerousRelease();
                }

                if (environmentBlock != IntPtr.Zero)
                {
                    _ = UserEnv.DestroyEnvironmentBlock(environmentBlock);
                }
            }
        }

        private static string BuildCommandLine(string executablePath, string? arguments)
        {
            var quotedPath = $"\"{executablePath}\"";
            return string.IsNullOrWhiteSpace(arguments) ? quotedPath : $"{quotedPath} {arguments}";
        }
    }
}
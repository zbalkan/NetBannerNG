using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Common.Extensions
{
    public static class ProcessStartInfoExtensions
    {
        /// <summary>
        /// Get the active user's token and run the process in the given user's context
        /// </summary>
        /// <see cref="https://stackoverflow.com/questions/1109271/launching-gui-app-from-windows-service-window-does-not-appear/1109443"/>
        /// <param name="psi"> Process to be started in user context </param>
        /// <returns> Returns true if succeeds. </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool RunAsActiveUser(this ProcessStartInfo psi)
        {
            if (psi == null)
            {
                throw new ArgumentNullException(nameof(psi));
            }

            var result = PrivilegeHelper.GetActiveUser(out var user) && psi.RunImpersonated(user!);
            user?.Dispose();
            return result;
        }

        /// <summary>
        /// Run the process in the given user context
        /// </summary>
        /// <see href="https://bytes.com/topic/c-sharp/answers/463942-using-openprocesstoken"/>
        /// <param name="psi"> process to be started </param>
        /// <param name="userIdentity"> user to impersonate </param>
        /// <returns> Returns true if succeeds. </returns>
        /// <exception cref="ArgumentNullException"><paramref name="psi"/> is <c>null</c>.</exception>
        private static bool RunImpersonated(this ProcessStartInfo psi, WindowsIdentity userIdentity)
        {
            if (psi == null)
            {
                throw new ArgumentNullException(nameof(psi));
            }

            if (userIdentity == null)
            {
                throw new ArgumentNullException(nameof(userIdentity));
            }

            Debug.WriteLine($"Before impersonation: {WindowsIdentity.GetCurrent().Name} ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");
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

            if (!NativeMethods.CreateProcessAsUser(userToken, // user token
                path, // executable path
                string.Empty, // arguments
                ref sa, // process security attributes ( none )
                ref sa, // thread  security attributes ( none )
                false, // inherit handles?
                0, // creation flags
                IntPtr.Zero, // environment variables
                dir, // current directory of the new process
                ref si, // startup info
                out var processInformation)) // receive process information in pi
            {
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to create process {psi.FileName} as user {userIdentity.Name}. Error code: {error}");
                return false;
            }

            try
            {
                Debug.WriteLine($"After impersonation: {WindowsIdentity.GetCurrent().Name} ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");
                return true;
            }
            finally
            {
                _ = NativeMethods.CloseHandle(processInformation.hThread);
                _ = NativeMethods.CloseHandle(processInformation.hProcess);
            }
        }
    }
}
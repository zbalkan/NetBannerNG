using NetBannerNG.Common.Native;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;

namespace NetBannerNG.Common.Extensions
{
    public static class ProcessExtensions
    {
        private const SecurityImpersonationLevel FullImpersonation = SecurityImpersonationLevel.TokenQuery |
                                                                     SecurityImpersonationLevel.TokenImpersonate |
                                                                     SecurityImpersonationLevel.TokenDuplicate;

        public static WindowsIdentity Owner(this Process process) => process == default ? throw new ArgumentNullException(nameof(process)) : new WindowsIdentity(process.GetUserToken());

        /// <summary>
        /// Run the process in the given user context
        /// </summary>
        /// <see href="https://bytes.com/topic/c-sharp/answers/463942-using-openprocesstoken"/>
        /// <param name="process"> process to be started </param>
        /// <param name="userIdentity"> user to impersonate </param>
        /// <returns> Returns true if succeeds. </returns>
        /// <exception cref="ArgumentNullException"><paramref name="process"/> is <c>null</c>.</exception>
        private static bool RunImpersonated(this Process process, WindowsIdentity userIdentity)
        {
            ArgumentNullException.ThrowIfNull(process);

            ArgumentNullException.ThrowIfNull(userIdentity);

            Console.WriteLine($"Before impersonation: {WindowsIdentity.GetCurrent().Name} ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");
            var userToken = userIdentity.AccessToken.DangerousGetHandle();

            var path = process.StartInfo.FileName;
            var dir = Path.GetDirectoryName(path);
            var si = new StartupInfo
            {
                lpDesktop = "winsta0\\default"
            };
            si.cb = Marshal.SizeOf(si);

            var pi = new ProcessInformation();
            var sa = new SecurityAttributes
            {
                bInheritHandle = 0
            };
            sa.nLength = Marshal.SizeOf(sa);
            sa.lpSecurityDescriptor = IntPtr.Zero;

            Console.WriteLine($"During impersonation: {userIdentity.Name}. ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");

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
                out pi)) // receive process information in pi
            {
                var error = Marshal.GetLastWin32Error();
                Console.WriteLine($"Failed to create process {process.ProcessName} as user {userIdentity.Name}. Error code: {error}");
                _ = NativeMethods.CloseHandle(userToken);
                return false;
            }
            Console.WriteLine($"After impersonation: {WindowsIdentity.GetCurrent().Name} ({(PrivilegeHelper.IsCurrentUserAdmin || PrivilegeHelper.IsSystem ? "Has privilege" : "No privilege")})");
            _ = NativeMethods.CloseHandle(userToken);
            return true;
        }

        private static IntPtr GetUserToken(this Process process)
        {
            var token = IntPtr.Zero;

            if (NativeMethods.OpenProcessToken(process.Handle, FullImpersonation, ref token) == 0)
            {
                throw new SecurityException($"Failed to access the token of the owner of {process.ProcessName}");
            }

            var duplicate = IntPtr.Zero;
            if (NativeMethods.DuplicateToken(token, (int)FullImpersonation, ref duplicate))
            {
                token = duplicate;
            }
            _ = NativeMethods.CloseHandle(duplicate);
            Console.WriteLine($"hToken: {token}");

            return token;
        }
    }
}
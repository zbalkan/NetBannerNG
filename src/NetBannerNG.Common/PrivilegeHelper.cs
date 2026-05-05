using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Common
{
    public static class PrivilegeHelper
    {
        private static bool? _isCurrentUserAdmin;
        private static bool? _isSessionOwnerAdmin;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsCurrentUserAdmin {
            get {
                if (_isCurrentUserAdmin.HasValue)
                {
                    return _isCurrentUserAdmin.Value;
                }

                _isCurrentUserAdmin = IsUserAdministrator(WindowsIdentity.GetCurrent());

                return _isCurrentUserAdmin.Value;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsSessionOwnerAdmin {
            get {
                if (_isSessionOwnerAdmin.HasValue)
                {
                    return _isSessionOwnerAdmin.Value;
                }

                if (Environment.UserInteractive)
                {
                    _isSessionOwnerAdmin = IsCurrentUserAdmin;
                    return _isSessionOwnerAdmin.Value;
                }

                if (!GetActiveUser(out var sessionOwner) || sessionOwner == null)
                {
                    _isSessionOwnerAdmin = false;
                    sessionOwner?.Dispose();
                    return _isSessionOwnerAdmin.Value;
                }

                _isSessionOwnerAdmin = IsUserAdministrator(sessionOwner);
                sessionOwner.Dispose();

                return _isSessionOwnerAdmin.Value;
            }
        }

        public static bool IsSystem => WindowsIdentity.GetCurrent().IsSystem;

        public static bool GetActiveUser(out WindowsIdentity? user)
        {
            var session = GetActiveSessionId();
            if (!NativeMethods.WTSQueryUserToken(session, out var userToken))
            {
                Debug.WriteLine($"Failed to query user token (Error no: {Marshal.GetLastWin32Error()})");
                user = null;
                NativeMethods.CloseHandle(userToken);
                return false;
            }

            user = new WindowsIdentity(userToken);
            NativeMethods.CloseHandle(userToken);
            return true;
        }

        public static bool IsUserAdministrator(WindowsIdentity user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            // This is the "Gold Standard" for checking local admin rights
            var principal = new WindowsPrincipal(user);

            // 1. Check for the local Built-in Administrator Role (covers S-1-5-32-544)
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                return true;
            }

            // 2. Check for Domain/Enterprise Admin SIDs if you need specific AD coverage
            // These are often not caught by WindowsBuiltInRole.Administrator on non-DCs
            return user.Groups.Any(g => g is SecurityIdentifier sid && (
                sid.IsWellKnown(WellKnownSidType.AccountDomainAdminsSid) ||
                sid.IsWellKnown(WellKnownSidType.AccountEnterpriseAdminsSid) ||
                sid.IsWellKnown(WellKnownSidType.AccountAdministratorSid)));
        }

        private static uint GetActiveSessionId()
        {
            var id = NativeMethods.WTSGetActiveConsoleSessionId();
            return id is 0xFFFFFFFF or 0
                ? throw new InvalidOperationException("No session attached to the physical console.")
                : id;
        }
    }
}
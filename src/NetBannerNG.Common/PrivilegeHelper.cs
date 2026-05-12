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
        private static uint? _cachedSessionId;

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
                var activeSessionId = GetInteractiveSessionId();
                if (_cachedSessionId != activeSessionId)
                {
                    _isSessionOwnerAdmin = null;
                    _cachedSessionId = activeSessionId;
                }

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

        public static bool TryGetActiveUserSid(out SecurityIdentifier? sid)
        {
            sid = null;
            if (GetActiveUser(out var user) && user != null)
            {
                sid = user.User;
                user.Dispose();
                return sid != null;
            }

            user?.Dispose();

            // In interactive debug mode (service host running as console app), WTSQueryUserToken
            // may fail while the current process identity is the actual interactive user.
            // Fall back to current identity SID so pipe ACLs still allow the launched UI client.
            if (Environment.UserInteractive)
            {
                sid = WindowsIdentity.GetCurrent().User;
                return sid != null;
            }

            return false;
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

        private static uint GetActiveSessionId() => GetInteractiveSessionId();

        public static void ResetSessionOwnerAdminCache()
        {
            _isSessionOwnerAdmin = null;
            _cachedSessionId = null;
        }

        [CLSCompliant(false)]
        public static uint GetInteractiveSessionId()
        {
            var id = NativeMethods.WTSGetActiveConsoleSessionId();
            if (id != 0xFFFFFFFF && id != 0)
            {
                return id;
            }

            var current = (uint)Process.GetCurrentProcess().SessionId;
            if (current != 0)
            {
                return current;
            }

            throw new InvalidOperationException("No interactive session detected.");
        }
    }
}
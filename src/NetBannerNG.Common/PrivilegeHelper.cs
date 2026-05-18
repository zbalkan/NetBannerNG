using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Common
{
    public static class PrivilegeHelper
    {
        private static readonly object CacheSync = new();
        private static bool? _isCurrentUserAdmin;
        private static bool? _isSessionOwnerAdmin;
        private static uint? _cachedSessionId;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsCurrentUserAdmin {
            get {
                lock (CacheSync)
                {
                    if (_isCurrentUserAdmin.HasValue)
                    {
                        return _isCurrentUserAdmin.Value;
                    }

                    _isCurrentUserAdmin = IsUserAdministrator(WindowsIdentity.GetCurrent());
                    return _isCurrentUserAdmin.Value;
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsSessionOwnerAdmin {
            get {
                var activeSessionId = GetInteractiveSessionId();
                lock (CacheSync)
                {
                    if (_cachedSessionId != activeSessionId)
                    {
                        _isSessionOwnerAdmin = null;
                        _cachedSessionId = activeSessionId;
                    }

                    if (_isSessionOwnerAdmin.HasValue)
                    {
                        return _isSessionOwnerAdmin.Value;
                    }
                }

                if (Environment.UserInteractive)
                {
                    lock (CacheSync)
                    {
                        _isSessionOwnerAdmin = IsCurrentUserAdmin;
                        return _isSessionOwnerAdmin.Value;
                    }
                }

                if (!GetActiveUser(out var sessionOwner) || sessionOwner == null)
                {
                    sessionOwner?.Dispose();
                    lock (CacheSync) { _isSessionOwnerAdmin = false; }
                    return false;
                }

                var result = IsUserAdministrator(sessionOwner);
                sessionOwner.Dispose();

                lock (CacheSync) { _isSessionOwnerAdmin = result; }
                return result;
            }
        }

        public static bool IsSystem => WindowsIdentity.GetCurrent().IsSystem;

        public static bool GetActiveUser(out WindowsIdentity? user) =>
            GetActiveUser(out user, out _);

        public static bool GetActiveUser(out WindowsIdentity? user, out int win32Error)
        {
            var session = GetActiveSessionId();
            if (!Wtsapi32.WTSQueryUserToken(session, out var userToken))
            {
                win32Error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to query user token (Error no: {win32Error})");
                user = null;
                return false;
            }

            try
            {
                user = new WindowsIdentity(userToken);
                win32Error = 0;
                return true;
            }
            finally
            {
                Kernel32.CloseHandle(userToken);
            }
        }

        public static bool TryGetActiveUserSid(out SecurityIdentifier? sid)
        {
            sid = null;

            // In interactive/debug host mode, the service process may be impersonating the active user.
            // Use process owner SID directly so ACLs follow the effective user context even when multiple
            // users are logged in.
            if (Environment.UserInteractive)
            {
                sid = WindowsIdentity.GetCurrent().User;
                return sid != null;
            }
            if (GetActiveUser(out var user) && user != null)
            {
                sid = user.User;
                user.Dispose();
                return sid != null;
            }

            user?.Dispose();


            var sessionId = GetActiveSessionId();
            if (TryGetSessionUserSid(sessionId, out sid) && sid != null)
            {
                return true;
            }

            // No valid session-owner SID could be resolved.
            // Do not fall back to the current process identity, because the service host identity
            // may differ from the active GUI user and would produce an ACL that denies the real client.
            return false;
        }

        private static bool TryGetSessionUserSid(uint sessionId, out SecurityIdentifier? sid)
        {
            sid = null;
            if (!TryGetSessionString(sessionId, Wtsapi32.WTSINFOCLASS.WTSUserName, out var userName) || string.IsNullOrWhiteSpace(userName))
            {
                return false;
            }

            _ = TryGetSessionString(sessionId, Wtsapi32.WTSINFOCLASS.WTSDomainName, out var domainName);
            var ntAccountName = string.IsNullOrWhiteSpace(domainName) ? userName : $"{domainName}\\{userName}";

            try
            {
                sid = (SecurityIdentifier)new NTAccount(ntAccountName).Translate(typeof(SecurityIdentifier));
                return true;
            }
            catch (IdentityNotMappedException)
            {
                return false;
            }
        }

        private static bool TryGetSessionString(uint sessionId, Wtsapi32.WTSINFOCLASS infoClass, out string value)
        {
            value = string.Empty;
            if (!Wtsapi32.WTSQuerySessionInformation(IntPtr.Zero, sessionId, infoClass, out var buffer, out _)
                || buffer == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                value = Marshal.PtrToStringUni(buffer) ?? string.Empty;
                return true;
            }
            finally
            {
                Wtsapi32.WTSFreeMemory(buffer);
            }
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
            lock (CacheSync)
            {
                _isSessionOwnerAdmin = null;
                _cachedSessionId = null;
            }
        }

        [CLSCompliant(false)]
        public static uint GetInteractiveSessionId()
        {
            // Prefer active WTS session so remote/terminal interactive users are handled correctly.
            if (TryGetActiveSessionIdFromWts(out var activeSessionId) && activeSessionId != 0)
            {
                return activeSessionId;
            }

            var consoleSessionId = Kernel32.WTSGetActiveConsoleSessionId();
            if (consoleSessionId != 0xFFFFFFFF && consoleSessionId != 0)
            {
                return consoleSessionId;
            }

            throw new InvalidOperationException("No interactive session detected.");
        }

        private static bool TryGetActiveSessionIdFromWts(out uint sessionId)
        {
            sessionId = 0;
            if (!Wtsapi32.WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var sessionInfoPtr, out var sessionCount)
                || sessionInfoPtr == IntPtr.Zero
                || sessionCount <= 0)
            {
                return false;
            }

            try
            {
                var dataSize = Marshal.SizeOf<Wtsapi32.WTSSESSIONINFO>();
                for (var i = 0; i < sessionCount; i++)
                {
                    var itemPtr = IntPtr.Add(sessionInfoPtr, i * dataSize);
                    var info = Marshal.PtrToStructure<Wtsapi32.WTSSESSIONINFO>(itemPtr);
                    if (info.State == Wtsapi32.WTSCONNECTSTATECLASS.WTSActive && info.SessionId != 0)
                    {
                        sessionId = info.SessionId;
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                Wtsapi32.WTSFreeMemory(sessionInfoPtr);
            }
        }
    }
} 
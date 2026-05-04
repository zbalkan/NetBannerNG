using NetBannerNG.Common.Native;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NetBannerNG.Common
{
    public static class PrivilegeHelper
    {
        private static bool? _isCurrentUserAdmin;
        private static bool? _isSessionOwnerAdmin;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsCurrentUserAdmin
        {
            get
            {
                if (_isCurrentUserAdmin.HasValue)
                {
                    return _isCurrentUserAdmin.Value;
                }

                _isCurrentUserAdmin = IsUserAdministrator(WindowsIdentity.GetCurrent());

                return _isCurrentUserAdmin.Value;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static bool IsSessionOwnerAdmin
        {
            get
            {
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

        private static bool DomainJoined => Environment.UserDomainName != Environment.MachineName && Environment.UserDomainName != "WORKGROUP";

        public static bool GetActiveUser(out WindowsIdentity? user)
        {
            var session = GetActiveSessionId();
            if (!NativeMethods.WTSQueryUserToken(session, out var userToken))
            {
                Console.WriteLine($"Failed to query user token (Error no: {Marshal.GetLastWin32Error()})");
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
            if (user == null) throw new ArgumentNullException(nameof(user));

            PrincipalContext? ctx = null;
            try
            {
                if (DomainJoined)
                {
                    var domain = Domain.GetComputerDomain();
                    try
                    {
                        ctx = new PrincipalContext(ContextType.Domain);
                    }
                    catch (PrincipalServerDownException)
                    {
                        // can't access domain, check local machine instead
                        ctx = new PrincipalContext(ContextType.Machine);
                    }
                }
                else
                {
                    // not in a domain
                    ctx = new PrincipalContext(ContextType.Machine);
                }
                // Slow method. ref: https://github.com/dotnet/runtime/issues/34598
                var up = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, user.Name);
                if (up == null) return false;
                var authGroups = up.GetAuthorizationGroups();
                return authGroups.Any(principal =>
                   principal.Sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
                   principal.Sid.IsWellKnown(WellKnownSidType.AccountDomainAdminsSid) ||
                   principal.Sid.IsWellKnown(WellKnownSidType.AccountAdministratorSid) ||
                   principal.Sid.IsWellKnown(WellKnownSidType.AccountEnterpriseAdminsSid));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
            finally
            {
                ctx?.Dispose();
            }
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

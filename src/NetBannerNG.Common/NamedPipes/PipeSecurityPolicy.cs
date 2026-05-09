using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeSecurityPolicy
    {
        private static readonly SecurityIdentifier LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier BuiltinAdministratorsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);

        public static PipeSecurity CreateDefaultServerSecurity(SecurityIdentifier? interactiveUserSid = null)
        {
            var pipeSecurity = new PipeSecurity();

            AddAllowRule(pipeSecurity, LocalSystemSid, PipeAccessRights.FullControl);
            AddAllowRule(pipeSecurity, BuiltinAdministratorsSid, PipeAccessRights.ReadWrite);

            if (interactiveUserSid != null)
            {
                AddAllowRule(pipeSecurity, interactiveUserSid, PipeAccessRights.ReadWrite);
            }

            return pipeSecurity;
        }

        private static void AddAllowRule(PipeSecurity pipeSecurity, SecurityIdentifier sid, PipeAccessRights rights) =>
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Allow));
    }
}

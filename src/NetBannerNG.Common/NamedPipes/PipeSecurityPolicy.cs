using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeSecurityPolicy
    {
        private static readonly SecurityIdentifier LocalServiceSid = new(WellKnownSidType.LocalServiceSid, null);
        private static readonly SecurityIdentifier NetworkSid = new(WellKnownSidType.NetworkSid, null);

        public static PipeSecurity CreateDefaultServerSecurity(SecurityIdentifier? interactiveUserSid = null)
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            AddAllowRule(pipeSecurity, LocalServiceSid, PipeAccessRights.FullControl);
            AddDenyRule(pipeSecurity, NetworkSid, PipeAccessRights.ReadWrite);

            if (interactiveUserSid != null)
            {
                AddAllowRule(pipeSecurity, interactiveUserSid, PipeAccessRights.ReadWrite);
            }

            return pipeSecurity;
        }

        private static void AddAllowRule(PipeSecurity pipeSecurity, SecurityIdentifier sid, PipeAccessRights rights) =>
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Allow));

        private static void AddDenyRule(PipeSecurity pipeSecurity, SecurityIdentifier sid, PipeAccessRights rights) =>
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Deny));
    }
}
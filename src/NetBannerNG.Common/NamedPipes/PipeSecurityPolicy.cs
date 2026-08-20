using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeSecurityPolicy
    {
        private static readonly SecurityIdentifier LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier NetworkSid = new(WellKnownSidType.NetworkSid, null);

        public static PipeSecurity CreateDefaultServerSecurity(SecurityIdentifier? interactiveUserSid = null)
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // IMPORTANT: keep ACL mutations at the PipeSecurity/PipeAccessRule layer.
            // Avoid raw security-descriptor surgery (RawSecurityDescriptor/CommonAce) here:
            // it is fragile across framework/runtime canonicalization and previously caused
            // runtime failures and connect-denied regressions.
            AddAllowRule(pipeSecurity, LocalSystemSid, PipeAccessRights.FullControl);
            AddDenyRule(pipeSecurity, NetworkSid, PipeAccessRights.ReadWrite);

            // Do not grant the generic INTERACTIVE SID. That SID represents every
            // interactively logged-on principal, not just the user in the session
            // this server is supervising. The resolved session-owner SID is the
            // only interactive principal that may use this pipe.
            if (interactiveUserSid != null)
            {
                AddInteractiveUserReadWriteRule(pipeSecurity, interactiveUserSid);
            }

            return pipeSecurity;
        }

        private static void AddAllowRule(PipeSecurity pipeSecurity, SecurityIdentifier sid, PipeAccessRights rights) =>
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Allow));

        private static void AddDenyRule(PipeSecurity pipeSecurity, SecurityIdentifier sid, PipeAccessRights rights) =>
            pipeSecurity.AddAccessRule(new PipeAccessRule(sid, rights, AccessControlType.Deny));

        private static void AddInteractiveUserReadWriteRule(PipeSecurity pipeSecurity, SecurityIdentifier sid) =>
            // Use PipeAccessRule rather than a raw ACE. In addition to ReadWrite, it
            // grants Synchronize, which is required by the pipe client connection path.
            // The grant remains restricted to the resolved active-session user SID.
            AddAllowRule(pipeSecurity, sid, PipeAccessRights.ReadWrite);
    }
}

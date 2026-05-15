using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeSecurityPolicy
    {
        private static readonly SecurityIdentifier LocalServiceSid = new(WellKnownSidType.LocalServiceSid, null);
        private static readonly SecurityIdentifier LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);
        private static readonly SecurityIdentifier NetworkSid = new(WellKnownSidType.NetworkSid, null);
        private static readonly SecurityIdentifier InteractiveSid = new(WellKnownSidType.InteractiveSid, null);

        public static PipeSecurity CreateDefaultServerSecurity(SecurityIdentifier? interactiveUserSid = null)
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            // IMPORTANT: keep ACL mutations at the PipeSecurity/PipeAccessRule layer.
            // Avoid raw security-descriptor surgery (RawSecurityDescriptor/CommonAce) here:
            // it is fragile across framework/runtime canonicalization and previously caused
            // runtime failures and connect-denied regressions.
            AddAllowRule(pipeSecurity, LocalSystemSid, PipeAccessRights.FullControl);
            AddAllowRule(pipeSecurity, LocalServiceSid, PipeAccessRights.FullControl);
            AddDenyRule(pipeSecurity, NetworkSid, PipeAccessRights.ReadWrite);
            AddAllowRule(pipeSecurity, InteractiveSid, PipeAccessRights.ReadWrite);

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

        private static void AddInteractiveUserReadWriteRule(PipeSecurity pipeSecurity, SecurityIdentifier sid)
        {
            // PipeAccessRule automatically includes Synchronize on allow rules.
            // Build the ACE directly to keep the interactive user grant at exact ReadWrite.
            var rawDescriptor = new RawSecurityDescriptor(pipeSecurity.GetSecurityDescriptorSddlForm(AccessControlSections.Access));
            rawDescriptor.DiscretionaryAcl ??= new RawAcl(GenericAcl.AclRevision, 1);

            var ace = new CommonAce(
                AceFlags.None,
                AceQualifier.AccessAllowed,
                (int)PipeAccessRights.ReadWrite,
                sid,
                isCallback: false,
                opaque: null);

            rawDescriptor.DiscretionaryAcl.InsertAce(rawDescriptor.DiscretionaryAcl.Count, ace);

            var binaryDescriptor = new byte[rawDescriptor.BinaryLength];
            rawDescriptor.GetBinaryForm(binaryDescriptor, 0);
            pipeSecurity.SetSecurityDescriptorBinaryForm(binaryDescriptor);
        }
    }
}

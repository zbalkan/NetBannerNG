using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeSecurityPolicy
    {
        public static PipeSecurity CreateDefaultServerSecurity()
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));
            return pipeSecurity;
        }
    }
}

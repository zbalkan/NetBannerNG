using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class PipeSecurityPolicyTests
    {
        [TestMethod]
        public void CreateDefaultServerSecurity_IncludesAuthenticatedUsersReadWriteAllowRule()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var authenticatedUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            var rule = rules.FirstOrDefault(r =>
                r.IdentityReference.Value == authenticatedUsersSid.Value &&
                r.AccessControlType == AccessControlType.Allow);

            Assert.IsNotNull(rule, "Expected explicit allow rule for Authenticated Users.");
            Assert.IsTrue((rule.PipeAccessRights & PipeAccessRights.ReadWrite) == PipeAccessRights.ReadWrite,
                "Expected Authenticated Users to have ReadWrite access.");
        }
    }
}

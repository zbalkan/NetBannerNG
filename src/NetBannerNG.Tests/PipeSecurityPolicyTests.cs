using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class PipeSecurityPolicyTests
    {
        [TestMethod]
        public void CreateDefaultServerSecurity_IncludesLocalServiceAllowRule()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            AssertAllowRule(rules, localServiceSid, PipeAccessRights.FullControl, "Local Service");
        }

        [TestMethod]
        public void CreateDefaultServerSecurity_DoesNotGrantBuiltinAdministratorsByDefault()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            AssertNoAllowRule(rules, adminsSid, "Builtin Administrators");
        }

        [TestMethod]
        public void CreateDefaultServerSecurity_ProtectsAclFromInheritance()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();

            Assert.IsTrue(security.AreAccessRulesProtected);
        }

        [TestMethod]
        public void CreateDefaultServerSecurity_AddsInteractiveUserWhenProvided()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity(sid);
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            AssertAllowRule(rules, sid, PipeAccessRights.ReadWrite, "interactive user SID");
        }

        [TestMethod]
        public void CreateDefaultServerSecurity_DoesNotGrantEveryoneOrAnonymousAccess()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var anonymousSid = new SecurityIdentifier(WellKnownSidType.AnonymousSid, null);

            AssertNoAllowRule(rules, everyoneSid, "Everyone");
            AssertNoAllowRule(rules, anonymousSid, "Anonymous");
        }

        [TestMethod]
        public void CreateDefaultServerSecurity_DeniesNetworkSidByDefault()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var networkSid = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
            var denyRule = rules.FirstOrDefault(r =>
                r.IdentityReference.Value == networkSid.Value &&
                r.AccessControlType == AccessControlType.Deny);

            Assert.IsNotNull(denyRule, "Expected explicit deny rule for Network SID.");
            Assert.AreEqual(PipeAccessRights.ReadWrite, denyRule.PipeAccessRights & PipeAccessRights.ReadWrite,
                "Expected Network SID deny rule to include ReadWrite access.");
        }

        [TestMethod]
        public void CreateDefaultServerSecurity_DoesNotGrantBuiltinUsersByDefault()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            AssertNoAllowRule(rules, usersSid, "Builtin Users");
        }

        private static void AssertAllowRule(
            System.Collections.Generic.IEnumerable<PipeAccessRule> rules,
            SecurityIdentifier sid,
            PipeAccessRights expectedRights,
            string principalLabel)
        {
            var rule = rules.FirstOrDefault(r =>
                r.IdentityReference.Value == sid.Value &&
                r.AccessControlType == AccessControlType.Allow);

            Assert.IsNotNull(rule, $"Expected explicit allow rule for {principalLabel}.");
            Assert.AreEqual(expectedRights, rule.PipeAccessRights & expectedRights,
                $"Expected {principalLabel} to have {expectedRights} access.");
        }

        private static void AssertNoAllowRule(
            System.Collections.Generic.IEnumerable<PipeAccessRule> rules,
            SecurityIdentifier sid,
            string principalLabel)
        {
            var hasAllowRule = rules.Any(r =>
                r.IdentityReference.Value == sid.Value &&
                r.AccessControlType == AccessControlType.Allow);

            Assert.IsFalse(hasAllowRule, $"Did not expect allow rule for {principalLabel}.");
        }
    }
}
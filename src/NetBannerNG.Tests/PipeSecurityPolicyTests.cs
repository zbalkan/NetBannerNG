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
        public void CreateDefaultServerSecurity_IncludesLocalSystemAndAdministratorsAllowRules()
        {
            var security = PipeSecurityPolicy.CreateDefaultServerSecurity();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<PipeAccessRule>()
                .ToList();

            var localSystemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            AssertAllowRule(rules, localSystemSid, PipeAccessRights.FullControl, "Local System");

            var adminsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            AssertAllowRule(rules, adminsSid, PipeAccessRights.ReadWrite, "Builtin Administrators");
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
            Assert.IsTrue((rule.PipeAccessRights & expectedRights) == expectedRights,
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
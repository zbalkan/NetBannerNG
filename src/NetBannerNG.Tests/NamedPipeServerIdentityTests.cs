using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Service;
using System.Security.Principal;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class NamedPipeServerIdentityTests
    {
        private sealed class SidConnection
        {
            public SecurityIdentifier UserSid { get; set; } = null!;
        }

        private sealed class SidTextConnection
        {
            public string UserSid { get; set; } = string.Empty;
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsTrue_ForMatchingSecurityIdentifier()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var connection = new SidConnection { UserSid = sid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, sid, out var userName);

            Assert.IsTrue(authorized);
            Assert.IsNull(userName);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_ForMismatchedSecurityIdentifier()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var otherSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var connection = new SidConnection { UserSid = otherSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, sid, out _);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsTrue_ForMatchingSidText()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var connection = new SidTextConnection { UserSid = sid.Value };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, sid, out _);

            Assert.IsTrue(authorized);
        }
    }
}

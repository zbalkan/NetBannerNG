using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Service;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class NamedPipeServerIdentityTests
    {
        private sealed class SidConnection
        {
            public object UserSid { get; set; } = null!;
        }

        private sealed class UserNameConnection
        {
            public string UserName { get; set; } = string.Empty;
        }

        private sealed class EmptyIdentityConnection
        {
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsTrue_ForMatchingSecurityIdentifier()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var connection = new SidConnection { UserSid = sid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, sid);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_ForMismatchedSecurityIdentifier()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var otherSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var connection = new SidConnection { UserSid = otherSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, sid);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsTrue_ForMatchingSidText()
        {
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var connection = new SidConnection { UserSid = sid.Value };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, sid);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_ForSystemSid_WhenActiveUserSidDiffers()
        {
            var activeUserSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var connection = new SidConnection { UserSid = systemSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_ForAdministratorSid_WhenActiveUserSidDiffers()
        {
            var activeUserSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var connection = new SidConnection { UserSid = adminSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_WhenOnlyUserNameIsExposed()
        {
            var activeUserSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var connection = new UserNameConnection { UserName = @"NT AUTHORITY\SYSTEM" };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: false);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsTrue_ForMatchingUserName_WhenInteractiveFallbackEnabled()
        {
            var identity = WindowsIdentity.GetCurrent();
            Assert.IsNotNull(identity?.User);
            var activeUserSid = identity!.User!;
            var connection = new UserNameConnection { UserName = identity.Name };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: true);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsTrue_WhenIdentityMetadataMissing_AndInteractiveFallbackEnabled()
        {
            var identity = WindowsIdentity.GetCurrent();
            Assert.IsNotNull(identity?.User);
            var activeUserSid = identity!.User!;
            var connection = new EmptyIdentityConnection();

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: true);

            Assert.IsTrue(authorized);
        }
    }
}
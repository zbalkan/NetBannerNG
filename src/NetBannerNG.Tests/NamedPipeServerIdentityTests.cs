using System;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Watchdog;

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

        private sealed class ImpersonationMethodConnection
        {
            public string ImpersonatedName { get; set; } = string.Empty;

            public string GetImpersonationUserName() => ImpersonatedName;
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
        public void TryAuthorizeClientIdentity_ReturnsFalse_ForMismatchedSecurityIdentifier_WhenFallbackEnabled()
        {
            var activeUserSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var otherSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            var connection = new SidConnection { UserSid = otherSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: true);

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
        public void TryAuthorizeClientIdentity_ReturnsTrue_ViaGetImpersonationUserNameMethod_NoFallback()
        {
            var identity = WindowsIdentity.GetCurrent();
            Assert.IsNotNull(identity?.User);
            var activeUserSid = identity!.User!;
            var connection = new ImpersonationMethodConnection { ImpersonatedName = identity.Name };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: false);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_WhenImpersonationUserNameDoesNotMatchActiveSid()
        {
            var activeUserSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var connection = new ImpersonationMethodConnection { ImpersonatedName = @"NT AUTHORITY\SYSTEM" };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: false);

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

        [TestMethod]
        public void TryAuthorizeClientIdentity_ReturnsFalse_ForDifferentUserName_WhenInteractiveFallbackEnabled()
        {
            var identity = WindowsIdentity.GetCurrent();
            Assert.IsNotNull(identity?.User);
            var activeUserSid = identity!.User!;
            var connection = new UserNameConnection { UserName = @"NT AUTHORITY\SYSTEM" };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeUserSid, allowInteractiveUserNameFallback: true);

            Assert.AreEqual(activeUserSid.IsWellKnown(WellKnownSidType.LocalSystemSid), authorized);
        }

        [TestMethod]
        public void EvaluateForwardedLogBudget_AllowsUpToLimitThenDropsOnceUntilWindowResets()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var windowStart = DateTime.MinValue;
            var count = 0;
            bool allowed;
            bool firstDrop;

            for (var i = 0; i < NamedPipeServer.MaxForwardedLogsPerWindow; i++)
            {
                (allowed, firstDrop, windowStart, count) = NamedPipeServer.EvaluateForwardedLogBudget(start, windowStart, count);
                Assert.IsTrue(allowed);
                Assert.IsFalse(firstDrop);
            }

            (allowed, firstDrop, windowStart, count) = NamedPipeServer.EvaluateForwardedLogBudget(start, windowStart, count);
            Assert.IsFalse(allowed);
            Assert.IsTrue(firstDrop);

            (allowed, firstDrop, windowStart, count) = NamedPipeServer.EvaluateForwardedLogBudget(start.AddSeconds(1), windowStart, count);
            Assert.IsFalse(allowed);
            Assert.IsFalse(firstDrop);

            (allowed, firstDrop, _, _) = NamedPipeServer.EvaluateForwardedLogBudget(start + NamedPipeServer.ForwardedLogWindow, windowStart, count);
            Assert.IsTrue(allowed);
            Assert.IsFalse(firstDrop);
        }

        [TestMethod]
        public void CreateClientForwardedLogEntry_UsesInformationSeverityAndSanitizesText()
        {
            var entry = NamedPipeServer.CreateClientForwardedLogEntry("netbannerng-pipe-s8", "Fullscreen restored\r\nnext");

            Assert.AreEqual(EventLogEntryType.Information, entry.Type);
            Assert.AreEqual(EventLogCatalog.PipeClientForwardedLog.EventId, entry.EventId);
            Assert.Contains("netbannerng-pipe-s8", entry.Message, StringComparison.Ordinal);
            Assert.Contains("Fullscreen restored\\r\\nnext", entry.Message, StringComparison.Ordinal);
            Assert.IsLessThan(0, entry.Message.IndexOf("Fullscreen restored\r\nnext", StringComparison.Ordinal));
        }
    }
}
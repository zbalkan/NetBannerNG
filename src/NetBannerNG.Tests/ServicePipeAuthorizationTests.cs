using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.NamedPipes;
using NetBannerNG.Service;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class ServicePipeAuthorizationTests
    {
        [TestMethod]
        public void IsAuthorizedClientConnection_ReturnsTrue_WhenPipeAndSessionMatch()
        {
            const uint sessionId = 4;
            var pipeName = PipeNaming.ForSession(sessionId);

            var authorized = NamedPipeServer.IsAuthorizedClientConnection(sessionId, pipeName, activeSessionId: sessionId);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void IsAuthorizedClientConnection_ReturnsFalse_WhenPipeNameDoesNotMatchExpectedSession()
        {
            const uint expectedSessionId = 2;
            const uint activeSessionId = 2;
            var wrongPipeName = PipeNaming.ForSession(7);

            var authorized = NamedPipeServer.IsAuthorizedClientConnection(expectedSessionId, wrongPipeName, activeSessionId);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void IsAuthorizedClientConnection_ReturnsFalse_WhenActiveSessionDiffers()
        {
            const uint expectedSessionId = 10;
            var pipeName = PipeNaming.ForSession(expectedSessionId);

            var authorized = NamedPipeServer.IsAuthorizedClientConnection(expectedSessionId, pipeName, activeSessionId: 11);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void IsAuthorizedClientConnection_ReturnsFalse_WhenPipeNameMissing(string? pipeName)
        {
            const uint expectedSessionId = 3;

            var authorized = NamedPipeServer.IsAuthorizedClientConnection(expectedSessionId, pipeName, activeSessionId: expectedSessionId);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public void IsAuthorizedClientConnection_IsCaseInsensitiveForPipeName()
        {
            const uint expectedSessionId = 8;
            var upperPipeName = PipeNaming.ForSession(expectedSessionId).ToUpperInvariant();

            var authorized = NamedPipeServer.IsAuthorizedClientConnection(expectedSessionId, upperPipeName, activeSessionId: expectedSessionId);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void HasSessionChanged_ReturnsTrue_OnlyWhenSessionIdDiffers()
        {
            Assert.IsFalse(ServiceHost.HasSessionChanged(5, 5));
            Assert.IsTrue(ServiceHost.HasSessionChanged(5, 6));
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_WhenSidMatches_ReturnsTrueAndSidDisplayImmediately()
        {
            var activeSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null);
            var connection = new { UserSid = activeSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeSid);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_WhenSidStringMatches_ReturnsTrueAndSidText()
        {
            var activeSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalServiceSid, null);
            var connection = new { UserSid = activeSid.Value };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeSid);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void TryAuthorizeClientIdentity_WhenSidMismatches_ReturnsFalse()
        {
            var activeSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalServiceSid, null);
            var otherSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.LocalSystemSid, null);
            var connection = new { UserSid = otherSid };

            var authorized = NamedPipeServer.TryAuthorizeClientIdentity(connection, activeSid);

            Assert.IsFalse(authorized);
        }

        [TestMethod]
        public async Task IsAuthorizedClientConnection_IsStableUnderConcurrentBurstChecks()
        {
            const uint sessionId = 12;
            var pipeName = PipeNaming.ForSession(sessionId);

            var tasks = Enumerable.Range(0, 2000).Select(i => Task.Run(() => {
                var isEven = i % 2 == 0;
                return isEven
                    ? NamedPipeServer.IsAuthorizedClientConnection(sessionId, pipeName, sessionId)
                    : !NamedPipeServer.IsAuthorizedClientConnection(sessionId, PipeNaming.ForSession(sessionId + 1), sessionId);
            }));

            var results = await Task.WhenAll(tasks);
            Assert.IsTrue(results.All(r => r));
        }
    }
}
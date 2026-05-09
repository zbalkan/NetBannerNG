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
        public void HasSessionChanged_ReturnsTrue_OnlyWhenSessionIdDiffers()
        {
            Assert.IsFalse(ServiceHost.HasSessionChanged(5, 5));
            Assert.IsTrue(ServiceHost.HasSessionChanged(5, 6));
        }
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Watchdog;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class NamedPipeServerConnectionTests
    {
        [TestMethod]
        public void IsAuthorizedConnectionInstance_ReturnsTrue_ForSameReference()
        {
            var connection = new object();

            var authorized = NamedPipeServer.IsAuthorizedConnectionInstance(connection, connection);

            Assert.IsTrue(authorized);
        }

        [TestMethod]
        public void IsAuthorizedConnectionInstance_ReturnsFalse_ForDifferentReferences()
        {
            var authorized = NamedPipeServer.IsAuthorizedConnectionInstance(new object(), new object());

            Assert.IsFalse(authorized);
        }
    }
}
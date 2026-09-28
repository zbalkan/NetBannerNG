using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Watchdog;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class ServiceProcessHelperTests
    {
        [TestMethod]
        [DataRow(1, 1u, true, true)]
        [DataRow(2, 1u, true, false)]
        [DataRow(1, 1u, false, false)]
        [DataRow(-1, 1u, true, false)]
        public void HasValidLaunchIdentity_RequiresRequestedSessionAndStartTime(int processSessionId, uint requestedSessionId, bool hasStartTime, bool expected)
        {
            var actual = ProcessHelper.HasValidLaunchIdentity(processSessionId, requestedSessionId, hasStartTime);

            Assert.AreEqual(expected, actual);
        }
    }
}
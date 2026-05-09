using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Service;

namespace NetBannerNG.Tests
{
    [TestClass]
    public sealed class ServiceHostWatchdogTests
    {
        [TestMethod]
        public void CalculateBackoffDelay_GrowsExponentiallyWithinBound()
        {
            var d1 = ServiceHost.CalculateBackoffDelay(1);
            var d2 = ServiceHost.CalculateBackoffDelay(2);
            var d3 = ServiceHost.CalculateBackoffDelay(3);

            Assert.IsTrue(d1.TotalSeconds >= 1 && d1.TotalSeconds < 1.6);
            Assert.IsTrue(d2.TotalSeconds >= 2 && d2.TotalSeconds < 2.6);
            Assert.IsTrue(d3.TotalSeconds >= 4 && d3.TotalSeconds < 4.6);
        }

        [TestMethod]
        public void CalculateBackoffDelay_IsBoundedAtThirtySecondsPlusJitter()
        {
            var delay = ServiceHost.CalculateBackoffDelay(50);
            Assert.IsTrue(delay.TotalSeconds >= 30 && delay.TotalSeconds < 30.6);
        }
    }
}
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;
using NetBannerNG.Common;
using NetBannerNG.Utils;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class MonitorLayoutPolicyTests
    {
        [TestMethod]
        public void GetVerticalTop_AddsBannerSizeToMonitorTop()
        {
            var monitor = new Monitor
            {
                Name = "DISPLAY1",
                Bounds = new Rect(100, 200, 1920, 1080),
                WorkingArea = new Rect(100, 200, 1920, 1040),
                IsPrimary = true
            };

            var top = MonitorLayoutPolicy.GetVerticalTop(monitor);

            Assert.AreEqual(monitor.Bounds.Top + Settings.Instance.BannerSize, top, 0.001);
        }

        [TestMethod]
        public void GetVerticalHeight_UsesBannerAndBorderOffsets()
        {
            var monitor = new Monitor
            {
                Name = "DISPLAY2",
                Bounds = new Rect(-1920, 0, 1920, 1080),
                WorkingArea = new Rect(-1920, 0, 1920, 1040),
                IsPrimary = false
            };

            var height = MonitorLayoutPolicy.GetVerticalHeight(monitor);

            var expected = monitor.Bounds.Height - Settings.Instance.BannerSize - Settings.Instance.BorderSize;
            Assert.AreEqual(expected, height, 0.001);
        }

        [TestMethod]
        public void GetVerticalHeight_ReturnsAtLeastOneForTinyMonitors()
        {
            var monitor = new Monitor
            {
                Name = "TINY",
                Bounds = new Rect(0, 0, 100, 1),
                WorkingArea = new Rect(0, 0, 100, 1),
                IsPrimary = false
            };

            var height = MonitorLayoutPolicy.GetVerticalHeight(monitor);

            Assert.AreEqual(1d, height, 0.001);
        }
    }
}

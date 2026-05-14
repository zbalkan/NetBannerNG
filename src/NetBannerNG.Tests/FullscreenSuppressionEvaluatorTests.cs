using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common;
using NetBannerNG.Utils;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class FullscreenSuppressionEvaluatorTests
    {
        [TestMethod]
        public void IsFullscreen_ReturnsTrue_WhenBoundsMatchMonitor()
        {
            var window = new MonitorRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            var monitor = new MonitorRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            Assert.IsTrue(FullscreenSuppressionEvaluator.IsFullscreen(window, monitor));
        }

        [TestMethod]
        public void EvaluateByGroup_UsesTopmostVisibleNonOwnWindowPerGroup()
        {
            var groupIds = new[] { "DISPLAY1", "DISPLAY2" };
            var boundsMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["0,0,1920,1080"] = "DISPLAY1",
                ["1920,0,1920,1080"] = "DISPLAY2"
            };
            var own = new HashSet<IntPtr> { new IntPtr(100) };
            var windows = new[]
            {
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(100), new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(101), new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(201), new MonitorRect{ Left=1920,Top=10,Right=3830,Bottom=1070}, new MonitorRect{ Left=1920,Top=0,Right=3840,Bottom=1080}, true)
            };

            var result = FullscreenSuppressionEvaluator.EvaluateByGroup(groupIds, boundsMap, own, windows);

            Assert.IsTrue(result["DISPLAY1"]);
            Assert.IsFalse(result["DISPLAY2"]);
        }

        [TestMethod]
        public void EvaluateByGroup_ReturnsTrue_WhenFullscreenWindowIsBehindNonFullscreenWindow()
        {
            var groupIds = new[] { "DISPLAY1" };
            var boundsMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["0,0,1920,1080"] = "DISPLAY1"
            };
            var own = new HashSet<IntPtr>();
            var windows = new[]
            {
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(301), new MonitorRect{ Left=100,Top=100,Right=1000,Bottom=700}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(302), new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true)
            };

            var result = FullscreenSuppressionEvaluator.EvaluateByGroup(groupIds, boundsMap, own, windows);

            Assert.IsTrue(result["DISPLAY1"]);
        }
    }
}

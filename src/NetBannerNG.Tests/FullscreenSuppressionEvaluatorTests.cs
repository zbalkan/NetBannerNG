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
        public void EvaluateByGroup_ReturnsFalse_WhenAnotherProcessWindowIsAboveFullscreenWindow()
        {
            var groupIds = new[] { "DISPLAY1" };
            var boundsMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["0,0,1920,1080"] = "DISPLAY1"
            };
            var own = new HashSet<IntPtr>();
            var windows = new[]
            {
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(301), new MonitorRect{ Left=100,Top=100,Right=1000,Bottom=700}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true, 11),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(302), new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true, 22)
            };

            var result = FullscreenSuppressionEvaluator.EvaluateByGroup(groupIds, boundsMap, own, windows);

            Assert.IsFalse(result["DISPLAY1"]);
        }

        [TestMethod]
        public void EvaluateByGroup_ReturnsTrue_WhenOnlyCompanionWindowsAreAboveFullscreenWindow()
        {
            var groupIds = new[] { "DISPLAY1" };
            var boundsMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["0,0,1920,1080"] = "DISPLAY1"
            };
            var own = new HashSet<IntPtr> { new IntPtr(300) };
            var windows = new[]
            {
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(300), new MonitorRect{ Left=0,Top=0,Right=200,Bottom=30}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true, 99),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(301), new MonitorRect{ Left=100,Top=900,Right=1800,Bottom=1000}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true, 22),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(302), new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true, 22)
            };

            var result = FullscreenSuppressionEvaluator.EvaluateByGroup(groupIds, boundsMap, own, windows);

            Assert.IsTrue(result["DISPLAY1"]);
        }

        [TestMethod]
        public void FindSuppressingWindowsByGroup_IgnoresWindowsOnOtherMonitors()
        {
            var boundsMap = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["0,0,1920,1080"] = "DISPLAY1",
                ["1920,0,1920,1080"] = "DISPLAY2"
            };
            var windows = new[]
            {
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(201), new MonitorRect{ Left=2000,Top=100,Right=2500,Bottom=600}, new MonitorRect{ Left=1920,Top=0,Right=3840,Bottom=1080}, true, 33),
                new FullscreenSuppressionEvaluator.WindowSnapshot(new IntPtr(101), new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, new MonitorRect{ Left=0,Top=0,Right=1920,Bottom=1080}, true, 44)
            };

            var result = FullscreenSuppressionEvaluator.FindSuppressingWindowsByGroup(boundsMap, new HashSet<IntPtr>(), windows);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(new IntPtr(101), result["DISPLAY1"].Handle);
        }

        [TestMethod]
        public void IsFullscreen_ReturnsFalse_WhenWindowIsSmallerThanMonitor()
        {
            var window = new MonitorRect { Left = 100, Top = 100, Right = 1000, Bottom = 700 };
            var monitor = new MonitorRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            Assert.IsFalse(FullscreenSuppressionEvaluator.IsFullscreen(window, monitor));
        }

        [TestMethod]
        public void IsFullscreen_ReturnsTrue_WhenBoundsMatchMonitor()
        {
            var window = new MonitorRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            var monitor = new MonitorRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            Assert.IsTrue(FullscreenSuppressionEvaluator.IsFullscreen(window, monitor));
        }

        [TestMethod]
        public void IsFullscreen_ReturnsTrue_WhenWindowExtendsBeyondMonitor()
        {
            // GetWindowRect on maximized borderless apps reports the invisible 7-8 px resize border,
            // so the window rectangle exceeds the monitor on every edge.
            var window = new MonitorRect { Left = -8, Top = -8, Right = 1928, Bottom = 1088 };
            var monitor = new MonitorRect { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
            Assert.IsTrue(FullscreenSuppressionEvaluator.IsFullscreen(window, monitor));
        }
    }
}
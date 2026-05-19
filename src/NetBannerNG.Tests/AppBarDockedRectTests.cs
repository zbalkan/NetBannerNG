using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common;
using NetBannerNG.Common.AppBar;

namespace NetBannerNG.Tests
{
    /// <summary>
    /// Behavioural tests for <see cref="AppBarDockedRect.Calculate"/>. Every test states a
    /// (monitor work area, bar thickness, dock edge) input and asserts the exact pixel
    /// rectangle the bar should occupy. The function operates purely in physical pixels;
    /// callers are responsible for DIP-to-pixel conversion before calling.
    /// </summary>
    [TestClass]
    public class AppBarDockedRectTests
    {
        // ============================================================
        // Primary monitor at origin, 100% DPI.
        // 1920x1200 monitor with a 48 px taskbar reserved at the bottom.
        // ============================================================

        [TestMethod]
        public void Top_PrimaryMonitor_PlacesBarFlushWithWorkAreaTop()
        {
            var workArea = Ltrb(0, 0, 1920, 1152);

            var rect = AppBarDockedRect.Calculate(DockEdge.Top, workArea, thicknessInPixels: 28);

            AssertRect(rect, left: 0, top: 0, right: 1920, bottom: 28);
        }

        [TestMethod]
        public void Bottom_PrimaryMonitor_PlacesBarFlushWithWorkAreaBottom()
        {
            var workArea = Ltrb(0, 0, 1920, 1152);

            var rect = AppBarDockedRect.Calculate(DockEdge.Bottom, workArea, thicknessInPixels: 4);

            AssertRect(rect, left: 0, top: 1148, right: 1920, bottom: 1152);
        }

        [TestMethod]
        public void Left_PrimaryMonitor_PlacesBarFlushWithWorkAreaLeft()
        {
            var workArea = Ltrb(0, 28, 1920, 1148);

            var rect = AppBarDockedRect.Calculate(DockEdge.Left, workArea, thicknessInPixels: 4);

            AssertRect(rect, left: 0, top: 28, right: 4, bottom: 1148);
        }

        [TestMethod]
        public void Right_PrimaryMonitor_PlacesBarFlushWithWorkAreaRight()
        {
            var workArea = Ltrb(0, 28, 1920, 1148);

            var rect = AppBarDockedRect.Calculate(DockEdge.Right, workArea, thicknessInPixels: 4);

            AssertRect(rect, left: 1916, top: 28, right: 1920, bottom: 1148);
        }

        // ============================================================
        // Regression: secondary monitor at NEGATIVE virtual-screen origin
        // with a higher DPI scaling factor.
        // 3840x2160 monitor at 300% scaling, positioned above-left of the
        // primary monitor (the reported user setup). Bar thicknesses are
        // already multiplied by the DPI factor by the caller -- the
        // calculator never sees DIPs.
        // ============================================================

        [TestMethod]
        public void Top_SecondaryMonitor_NegativeOriginHighDpi_PlacesBarAtMonitorTop()
        {
            // Work area starts at the very top of the monitor (no top taskbar);
            // 72 px reserved at the bottom for a secondary-monitor taskbar.
            var workArea = Ltrb(-1006, -2160, 2834, -72);
            const int bannerHeightPx = 84; // 28 DIPs * 3.0 DPI factor

            var rect = AppBarDockedRect.Calculate(DockEdge.Top, workArea, bannerHeightPx);

            AssertRect(rect, left: -1006, top: -2160, right: 2834, bottom: -2076);
        }

        [TestMethod]
        public void Bottom_SecondaryMonitor_NegativeOriginHighDpi_PlacesBarAtWorkAreaBottom()
        {
            var workArea = Ltrb(-1006, -2160, 2834, -72);
            const int borderHeightPx = 12; // 4 DIPs * 3.0

            var rect = AppBarDockedRect.Calculate(DockEdge.Bottom, workArea, borderHeightPx);

            AssertRect(rect, left: -1006, top: -84, right: 2834, bottom: -72);
        }

        [TestMethod]
        public void Left_SecondaryMonitor_NegativeOriginHighDpi_PlacesBarAtMonitorLeft()
        {
            var workArea = Ltrb(-1006, -2076, 2834, -84);
            const int borderWidthPx = 12;

            var rect = AppBarDockedRect.Calculate(DockEdge.Left, workArea, borderWidthPx);

            AssertRect(rect, left: -1006, top: -2076, right: -994, bottom: -84);
        }

        [TestMethod]
        public void Right_SecondaryMonitor_NegativeOriginHighDpi_PlacesBarAtMonitorRight()
        {
            var workArea = Ltrb(-1006, -2076, 2834, -84);
            const int borderWidthPx = 12;

            var rect = AppBarDockedRect.Calculate(DockEdge.Right, workArea, borderWidthPx);

            AssertRect(rect, left: 2822, top: -2076, right: 2834, bottom: -84);
        }

        // ============================================================
        // Invariants that must hold for every monitor configuration.
        // The bug from the original report violated these on the 300%
        // secondary monitor: the bar's pixel rect ended up 3x wider than
        // the work area and the Top edge was off the screen entirely.
        // ============================================================

        [TestMethod]
        public void Top_AnyMonitor_BarSpansFullWorkAreaWidthAndStartsAtWorkAreaTop()
        {
            foreach (var (workArea, thickness) in EveryRepresentativeMonitor())
            {
                var rect = AppBarDockedRect.Calculate(DockEdge.Top, workArea, thickness);

                Assert.AreEqual(workArea.Left, rect.Left, $"Top bar must span work-area left for {workArea}");
                Assert.AreEqual(workArea.Right, rect.Right, $"Top bar must span work-area right for {workArea}");
                Assert.AreEqual(workArea.Top, rect.Top, $"Top bar must align with work-area top for {workArea}");
                Assert.AreEqual(thickness, rect.Height, $"Top bar height must equal requested thickness for {workArea}");
            }
        }

        [TestMethod]
        public void Bottom_AnyMonitor_BarSpansFullWorkAreaWidthAndEndsAtWorkAreaBottom()
        {
            foreach (var (workArea, thickness) in EveryRepresentativeMonitor())
            {
                var rect = AppBarDockedRect.Calculate(DockEdge.Bottom, workArea, thickness);

                Assert.AreEqual(workArea.Left, rect.Left, $"Bottom bar must span work-area left for {workArea}");
                Assert.AreEqual(workArea.Right, rect.Right, $"Bottom bar must span work-area right for {workArea}");
                Assert.AreEqual(workArea.Bottom, rect.Bottom, $"Bottom bar must align with work-area bottom for {workArea}");
                Assert.AreEqual(thickness, rect.Height, $"Bottom bar height must equal requested thickness for {workArea}");
            }
        }

        [TestMethod]
        public void Left_AnyMonitor_BarSpansFullWorkAreaHeightAndStartsAtWorkAreaLeft()
        {
            foreach (var (workArea, thickness) in EveryRepresentativeMonitor())
            {
                var rect = AppBarDockedRect.Calculate(DockEdge.Left, workArea, thickness);

                Assert.AreEqual(workArea.Top, rect.Top, $"Left bar must span work-area top for {workArea}");
                Assert.AreEqual(workArea.Bottom, rect.Bottom, $"Left bar must span work-area bottom for {workArea}");
                Assert.AreEqual(workArea.Left, rect.Left, $"Left bar must align with work-area left for {workArea}");
                Assert.AreEqual(thickness, rect.Width, $"Left bar width must equal requested thickness for {workArea}");
            }
        }

        [TestMethod]
        public void Right_AnyMonitor_BarSpansFullWorkAreaHeightAndEndsAtWorkAreaRight()
        {
            foreach (var (workArea, thickness) in EveryRepresentativeMonitor())
            {
                var rect = AppBarDockedRect.Calculate(DockEdge.Right, workArea, thickness);

                Assert.AreEqual(workArea.Top, rect.Top, $"Right bar must span work-area top for {workArea}");
                Assert.AreEqual(workArea.Bottom, rect.Bottom, $"Right bar must span work-area bottom for {workArea}");
                Assert.AreEqual(workArea.Right, rect.Right, $"Right bar must align with work-area right for {workArea}");
                Assert.AreEqual(thickness, rect.Width, $"Right bar width must equal requested thickness for {workArea}");
            }
        }

        // ============================================================
        // Pathological inputs.
        // ============================================================

        [TestMethod]
        public void NegativeThickness_ClampedToZero()
        {
            var workArea = Ltrb(0, 0, 1920, 1080);

            var rect = AppBarDockedRect.Calculate(DockEdge.Top, workArea, thicknessInPixels: -7);

            Assert.AreEqual(0, rect.Height);
            Assert.AreEqual(workArea.Top, rect.Top);
        }

        [TestMethod]
        public void Edge_None_ReturnsWorkAreaUnchanged()
        {
            var workArea = Ltrb(-1006, -2160, 2834, -72);

            var rect = AppBarDockedRect.Calculate(DockEdge.None, workArea, thicknessInPixels: 84);

            Assert.AreEqual(workArea, rect);
        }

        // ============================================================
        // helpers
        // ============================================================

        private static MonitorRect Ltrb(int left, int top, int right, int bottom) =>
            new() { Left = left, Top = top, Right = right, Bottom = bottom };

        private static void AssertRect(MonitorRect actual, int left, int top, int right, int bottom)
        {
            Assert.AreEqual(left, actual.Left, "Left");
            Assert.AreEqual(top, actual.Top, "Top");
            Assert.AreEqual(right, actual.Right, "Right");
            Assert.AreEqual(bottom, actual.Bottom, "Bottom");
        }

        private static System.Collections.Generic.IEnumerable<(MonitorRect workArea, int thickness)> EveryRepresentativeMonitor()
        {
            // Primary monitor 1920x1200 @ 100% with bottom taskbar.
            yield return (Ltrb(0, 0, 1920, 1152), 28);
            // Secondary 3840x2160 @ 300% above-left of primary.
            yield return (Ltrb(-1006, -2160, 2834, -72), 84);
            // Secondary 2560x1440 @ 150% to the right of primary.
            yield return (Ltrb(1920, 0, 4480, 1440), 42);
            // Monitor with only a tiny work area (extreme).
            yield return (Ltrb(0, 0, 100, 50), 4);
        }
    }
}

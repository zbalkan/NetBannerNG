using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class BorderManagerTests
    {
        [TestMethod]
        public void BuildGroupId_UsesMonitorName_WhenPresent()
        {
            var id = BorderManager.BuildGroupId("\\.\\DISPLAY1", new Rect(0, 0, 1920, 1080));
            Assert.AreEqual("\\.\\DISPLAY1", id);
        }

        [TestMethod]
        public void BuildGroupId_FallsBackToBounds_WhenNameMissing()
        {
            var id = BorderManager.BuildGroupId("", new Rect(10, 20, 1280, 720));
            Assert.AreEqual("10,20,1280,720", id);
        }

        [TestMethod]
        public void HasMonitorLayoutChanged_ReturnsFalse_WhenLayoutMatches()
        {
            var changed = BorderManager.HasMonitorLayoutChanged(
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                true,
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                true);

            Assert.IsFalse(changed);
        }

        [TestMethod]
        public void HasMonitorLayoutChanged_ReturnsTrue_WhenBoundsChange()
        {
            var changed = BorderManager.HasMonitorLayoutChanged(
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                false,
                new Rect(1920, 0, 1920, 1080),
                new Rect(1920, 0, 1920, 1040),
                false);

            Assert.IsTrue(changed);
        }

        [TestMethod]
        public void HasMonitorLayoutChanged_ReturnsTrue_WhenWorkingAreaChange()
        {
            var changed = BorderManager.HasMonitorLayoutChanged(
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                false,
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1000),
                false);

            Assert.IsTrue(changed);
        }

        [TestMethod]
        public void HasMonitorLayoutChanged_ReturnsTrue_WhenPrimaryChanges()
        {
            var changed = BorderManager.HasMonitorLayoutChanged(
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                true,
                new Rect(0, 0, 1920, 1080),
                new Rect(0, 0, 1920, 1040),
                false);

            Assert.IsTrue(changed);
        }
    }
}

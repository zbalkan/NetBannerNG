using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class MonitorRectTests
    {
        [TestMethod]
        public void WidthHeight_AreCalculatedFromEdges()
        {
            var rect = new MonitorRect { Left = 10, Top = 15, Right = 110, Bottom = 65 };

            Assert.AreEqual(100, rect.Width);
            Assert.AreEqual(50, rect.Height);
        }

        [TestMethod]
        public void SettingXAndY_MovesRectWithoutChangingSize()
        {
            var rect = new MonitorRect
            {
                Left = 10,
                Top = 20,
                Right = 30,
                Bottom = 60,
                X = 100,
                Y = 200
            };

            Assert.AreEqual(100, rect.Left);
            Assert.AreEqual(200, rect.Top);
            Assert.AreEqual(120, rect.Right);
            Assert.AreEqual(240, rect.Bottom);
        }

        [TestMethod]
        public void SettingWidthAndHeight_UpdatesRightAndBottom()
        {
            var rect = new MonitorRect { Left = 5, Top = 6, Right = 7, Bottom = 9 };

            rect.Width = 80;
            rect.Height = 40;

            Assert.AreEqual(85, rect.Right);
            Assert.AreEqual(46, rect.Bottom);
        }

        [TestMethod]
        public void EqualityOperators_WorkCorrectly()
        {
            var a = new MonitorRect { Left = 1, Top = 2, Right = 3, Bottom = 4 };
            var b = new MonitorRect { Left = 1, Top = 2, Right = 3, Bottom = 4 };
            var c = new MonitorRect { Left = 0, Top = 2, Right = 3, Bottom = 4 };

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);

            Assert.IsFalse(a.Equals(c));
            Assert.IsFalse(a == c);
            Assert.IsTrue(a != c);
        }
    }
}

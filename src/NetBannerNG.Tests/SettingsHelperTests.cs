using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class SettingsHelperTests
    {
        [TestMethod]
        public void MapClassification_UsesUppercaseLabels()
        {
            Assert.AreEqual("UNCLASSIFIED", InvokePrivate<string>("MapClassification", 1));
            Assert.AreEqual("SECRET", InvokePrivate<string>("MapClassification", 2));
            Assert.AreEqual("TOP SECRET", InvokePrivate<string>("MapClassification", 3));
            Assert.AreEqual("SCI", InvokePrivate<string>("MapClassification", 4));
            Assert.AreEqual("PUBLIC", InvokePrivate<string>("MapClassification", 99));
        }

        [TestMethod]
        public void ComposeClassificationText_IncludesConfiguredPolicyFragmentsOnly()
        {
            var composed = InvokePrivate<string>(
                "ComposeClassificationText",
                "Secret",
                "NOFORN",
                3,
                2,
                1,
                "REL TO USA");

            Assert.AreEqual("Secret | NOFORN | INFOCON 3 | FPCON 2 | CPCON 1 | REL TO USA", composed);

            var minimal = InvokePrivate<string>(
                "ComposeClassificationText",
                "Unclassified",
                " ",
                0,
                0,
                0,
                "");

            Assert.AreEqual("Unclassified", minimal);
        }

        [TestMethod]
        public void ToBackgroundHex_UsesExpectedFallback_ForUnknownValues()
        {
            Assert.AreEqual("#007A33", InvokePrivate<string>("ToBackgroundHex", 999));
            Assert.AreEqual("#0033A0", InvokePrivate<string>("ToBackgroundHex", 2));
            Assert.AreEqual("#FF671F", InvokePrivate<string>("ToBackgroundHex", 9));
        }

        private static T InvokePrivate<T>(string method, params object[] args)
        {
            var type = Type.GetType("NetBannerNG.Settings, NetBannerNG")
                ?? throw new InvalidOperationException("Type NetBannerNG.Settings was not found.");
            var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Method {method} was not found.");

            return (T)(methodInfo.Invoke(null, args)
                ?? throw new InvalidOperationException($"Method {method} returned null."));
        }

    }
}

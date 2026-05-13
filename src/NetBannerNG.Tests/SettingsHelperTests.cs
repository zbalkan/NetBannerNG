using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public void ParseClassification_UsesLegacyClassificationValues()
        {
            Assert.AreEqual(1, InvokePrivate<int>("ParseClassification", "UNCLASSIFIED"));
            Assert.AreEqual(2, InvokePrivate<int>("ParseClassification", "SECRET"));
            Assert.AreEqual(3, InvokePrivate<int>("ParseClassification", "TOP SECRET"));
            Assert.AreEqual(4, InvokePrivate<int>("ParseClassification", "SCI"));
            Assert.AreEqual(1, InvokePrivate<int>("ParseClassification", "NATO SECRET"));
        }

        [TestMethod]
        public void ResolveCatalogBackground_UsesSelectedProfile()
        {
            Assert.AreEqual("#F7EA48", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "SECRET | COSMIC TOP SECRET"));
            Assert.AreEqual("#C8102E", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "NATO SECRET | REL TO USA"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "UK", "UK TOP SECRET | UK EYES ONLY"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "MISSION USE"));
        }

        [TestMethod]
        public void ResolveCatalogBackground_ValidatesPublishedCatalogColors()
        {
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "NATO UNCLASSIFIED"));
            Assert.AreEqual("#FF671F", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "NATO RESTRICTED"));
            Assert.AreEqual("#0033A0", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "NATO CONFIDENTIAL"));
            Assert.AreEqual("#C8102E", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "NATO SECRET"));
            Assert.AreEqual("#F7EA48", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "COSMIC TOP SECRET"));

            Assert.AreEqual("#502B85", InvokePrivate<string>("ResolveCatalogBackground", "US", "CUI"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "US", "UNCLASSIFIED"));
            Assert.AreEqual("#0033A0", InvokePrivate<string>("ResolveCatalogBackground", "US", "CONFIDENTIAL"));
            Assert.AreEqual("#C8102E", InvokePrivate<string>("ResolveCatalogBackground", "US", "SECRET"));
            Assert.AreEqual("#FF8C00", InvokePrivate<string>("ResolveCatalogBackground", "US", "TOP SECRET"));
            Assert.AreEqual("#FCE83A", InvokePrivate<string>("ResolveCatalogBackground", "US", "TS//SCI"));

            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "UK", "UK TOP SECRET"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "CA", "PROTECTED B"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "AU", "SECRET"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "DE", "VS-NUR FÜR DEN DIENSTGEBRAUCH"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "EUCI", "SECRET UE / EU SECRET"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "EP", "R-UE / EU-R"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "ESA", "ESA SECRET"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "UN", "STRICTLY CONFIDENTIAL"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "DE", "VS-VERTRAULICH"));
            Assert.AreEqual("#007A33", InvokePrivate<string>("ResolveCatalogBackground", "DK", "HEMMELIGT"));
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
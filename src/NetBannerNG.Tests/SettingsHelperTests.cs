using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetBannerNG.Tests
{
    [TestClass]
    public class SettingsHelperTests
    {
        [TestMethod]
        public void ComposeClassificationText_IncludesConfiguredPolicyFragmentsOnly()
        {
            var composed = InvokePrivate<string>(
                "ComposeClassificationText",
                "Secret",
                "NOFORN",
                3,
                2,
                "REL TO USA");

            Assert.AreEqual("Secret | NOFORN | INFOCON 3 | FPCON 2 | REL TO USA", composed);

            var minimal = InvokePrivate<string>(
                "ComposeClassificationText",
                "Unclassified",
                " ",
                0,
                0,
                "");

            Assert.AreEqual("Unclassified", minimal);
        }

        [TestMethod]
        public void ResolveCatalogBackground_UsesSelectedProfile()
        {
            Assert.AreEqual("#F7EA48", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "SECRET | COSMIC TOP SECRET"));
            Assert.AreEqual("#C8102E", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "NATO SECRET | REL TO USA"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "UK", "UK TOP SECRET | UK EYES ONLY"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "NATO", "MISSION USE"));
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

            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "UK", "UK TOP SECRET"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "CA", "PROTECTED B"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "AU", "SECRET"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "DE", "VS-NUR FÜR DEN DIENSTGEBRAUCH"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "EUCI", "SECRET UE / EU SECRET"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "EP", "R-UE / EU-R"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "ESA", "ESA SECRET"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "UN", "STRICTLY CONFIDENTIAL"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "DE", "VS-VERTRAULICH"));
            Assert.AreEqual("#FFFFFF", InvokePrivate<string>("ResolveCatalogBackground", "DK", "HEMMELIGT"));
        }

        [TestMethod]
        public void Clamp_EnforcesExpectedNumericRanges()
        {
            Assert.AreEqual(16, InvokePrivate<int>("Clamp", 5, 16, 60));
            Assert.AreEqual(60, InvokePrivate<int>("Clamp", 90, 16, 60));
            Assert.AreEqual(28, InvokePrivate<int>("Clamp", 28, 16, 60));
        }

        [TestMethod]
        public void Truncate_EnforcesMaxLength_ForCaveatsPayloads()
        {
            var overLimit = new string('X', 50);
            var truncated = InvokePrivate<string>("Truncate", overLimit, 40);

            Assert.AreEqual(40, truncated.Length);
            Assert.AreEqual(overLimit.Substring(0, 40), truncated);
            Assert.AreEqual("short", InvokePrivate<string>("Truncate", "short", 40));
        }

        [TestMethod]
        public void ParseClassificationSelection_HandlesMissingSeparatorAsNotConfigured()
        {
            var parsed = InvokePrivate<ValueTuple<string, string>>("ParseClassificationSelection", "INVALID");
            Assert.AreEqual("NOT_CONFIGURED", parsed.Item1);
            Assert.AreEqual("Classification not configured", parsed.Item2);
        }

        [TestMethod]
        public void TryConvertToInt_ReturnsFalse_ForInvalidInput()
        {
            var type = Type.GetType("NetBannerNG.SettingsRegistryReader, NetBannerNG")
                ?? throw new InvalidOperationException("Type NetBannerNG.SettingsRegistryReader was not found.");
            var methodInfo = type.GetMethod("TryConvertToInt", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Method TryConvertToInt was not found.");

            var args = new object?[] { "not-an-int", 0 };
            var success = (bool)(methodInfo.Invoke(null, args) ?? false);

            Assert.IsFalse(success);
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
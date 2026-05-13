using System;
using System.Reflection;
using Microsoft.Win32;
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
        public void ResolvePolicyKey_PrefersNetBannerNg_WhenBothPolicyRootsExist()
        {
            using var scope = new RegistryTestScope();
            var netBannerNgPath = scope.ResolveManagedPolicyPath("NetBannerNG");
            var legacyPath = scope.ResolveManagedPolicyPath("NetBanner");

            using (var netBannerNgKey = Registry.CurrentUser.CreateSubKey(netBannerNgPath, true))
            using (var legacyKey = Registry.CurrentUser.CreateSubKey(legacyPath, true))
            {
                netBannerNgKey!.SetValue("Classification", 2, RegistryValueKind.DWord);
                netBannerNgKey.SetValue("CustomDisplayText", "FROM_NEW", RegistryValueKind.String);
                legacyKey!.SetValue("Classification", 3, RegistryValueKind.DWord);
                legacyKey.SetValue("CustomDisplayText", "FROM_LEGACY", RegistryValueKind.String);
            }

            using var policyRoot = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var resolved = InvokePrivate<RegistryKey?>("ResolvePolicyKey", policyRoot);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(2, Convert.ToInt32(resolved!.GetValue("Classification")));
            Assert.AreEqual("FROM_NEW", resolved.GetValue("CustomDisplayText")?.ToString());
        }

        [TestMethod]
        public void ResolvePolicyKey_WritesDefaultValues_WhenNetBannerNgValuesMissing()
        {
            using var scope = new RegistryTestScope();
            var netBannerNgPath = scope.ResolveManagedPolicyPath("NetBannerNG");

            using var policyRoot = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var resolved = InvokePrivate<RegistryKey?>("ResolvePolicyKey", policyRoot);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(0, Convert.ToInt32(resolved!.GetValue("CustomSettings")));
            Assert.AreEqual($"NOT CONFIGURED - Classification not configured", resolved.GetValue("ClassificationSelection")?.ToString());

            using var migratedKey = Registry.CurrentUser.OpenSubKey(netBannerNgPath, false);
            Assert.IsNotNull(migratedKey);
            Assert.AreEqual(0, Convert.ToInt32(migratedKey!.GetValue("CustomSettings")));
            Assert.AreEqual($"NOT CONFIGURED - Classification not configured", migratedKey.GetValue("ClassificationSelection")?.ToString());
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

        private sealed class RegistryTestScope : IDisposable
        {
            private const string NetBannerNgPath = @"SOFTWARE\Policies\NetbannerNG";
            private const string LegacyPath = @"SOFTWARE\Policies\Microsoft\NetBanner";

            internal RegistryTestScope()
            {
                Registry.CurrentUser.DeleteSubKeyTree(NetBannerNgPath, false);
                Registry.CurrentUser.DeleteSubKeyTree(LegacyPath, false);
            }

            internal string ResolveManagedPolicyPath(string productName)
                => productName == "NetBannerNG" ? NetBannerNgPath : LegacyPath;

            public void Dispose()
            {
                Registry.CurrentUser.DeleteSubKeyTree(NetBannerNgPath, false);
                Registry.CurrentUser.DeleteSubKeyTree(LegacyPath, false);
            }
        }

    }
}

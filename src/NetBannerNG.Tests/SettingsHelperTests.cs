using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
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

        [TestMethod]
        public async Task TryParseBrush_IsThreadSafe_UnderConcurrentCalls()
        {
            var settingsType = Type.GetType("NetBannerNG.Settings, NetBannerNG")
                ?? throw new InvalidOperationException("Type NetBannerNG.Settings was not found.");
            var methodInfo = settingsType.GetMethod("TryParseBrush", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("Method TryParseBrush was not found.");

            var inputs = new[] { "#FF0000", "#00FF00", "", "bad-color", null, "#112233" };

            var tasks = Enumerable.Range(0, 200)
                .Select(i => Task.Run(() => {
                    var input = inputs[i % inputs.Length];
                    var args = new object?[] { input!, null! };
                    var result = (bool)(methodInfo.Invoke(null, args) ?? false);
                    var brush = args[1] as SolidColorBrush;

                    Assert.IsNotNull(brush);
                    if (string.IsNullOrWhiteSpace(input) || string.Equals(input, "bad-color", StringComparison.Ordinal))
                    {
                        Assert.IsFalse(result);
                    }
                }, TestContext.CancellationToken));

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            await Task.WhenAll(tasks);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
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

        public TestContext TestContext { get; set; }
    }
}

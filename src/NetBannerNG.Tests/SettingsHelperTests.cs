using NetBannerNG.Utils;

namespace NetBannerNG.Tests;

[TestClass]
public class SettingsHelperTests
{
    [TestMethod]
    public void MapClassification_UsesLegacyAdmxLabels()
    {
        Assert.AreEqual("Unclassified", InvokePrivate<string>("MapClassification", 1));
        Assert.AreEqual("Secret", InvokePrivate<string>("MapClassification", 2));
        Assert.AreEqual("Top Secret", InvokePrivate<string>("MapClassification", 3));
        Assert.AreEqual("SCI", InvokePrivate<string>("MapClassification", 4));
        Assert.AreEqual("Public", InvokePrivate<string>("MapClassification", 99));
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
    public void ParseEnumValue_FallsBackToDefault_ForOutOfRangeAndInvalidInputs()
    {
        Assert.AreEqual(
            CustomBackgroundColors.Green,
            InvokePrivateGeneric<CustomBackgroundColors>("ParseEnumValue", "999", CustomBackgroundColors.Green));

        Assert.AreEqual(
            CustomForeColors.White,
            InvokePrivateGeneric<CustomForeColors>("ParseEnumValue", "invalid", CustomForeColors.White));

        Assert.AreEqual(
            CustomForeColors.White,
            InvokePrivateGeneric<CustomForeColors>("ParseEnumValue", -1, CustomForeColors.White));
    }

    private static T InvokePrivate<T>(string method, params object[] args)
    {
        var type = typeof(SettingsHelper);
        var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method {method} was not found.");

        return (T)(methodInfo.Invoke(null, args)
            ?? throw new InvalidOperationException($"Method {method} returned null."));
    }

    private static TEnum InvokePrivateGeneric<TEnum>(string method, object rawValue, TEnum defaultValue) where TEnum : struct, Enum
    {
        var type = typeof(SettingsHelper);
        var methodInfo = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method {method} was not found.");

        var closedMethod = methodInfo.MakeGenericMethod(typeof(TEnum));
        return (TEnum)(closedMethod.Invoke(null, [rawValue, defaultValue])
            ?? throw new InvalidOperationException($"Method {method} returned null."));
    }
}
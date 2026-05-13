using System;
using System.Globalization;
using Microsoft.Win32;

namespace NetBannerNG;

internal sealed class SettingsRegistryReader
{
    private readonly RegistryKey _localMachineKey;

    internal SettingsRegistryReader(RegistryKey localMachineKey)
    {
        _localMachineKey = localMachineKey;
    }


    [System.Diagnostics.Conditional("DEBUG")]
    internal void EnsureDebugRegistryKeys(string policyPath, string localPath)
    {
        using var _ = _localMachineKey.CreateSubKey(policyPath, true);
        using var localKey = _localMachineKey.CreateSubKey(localPath, true);
        if (localKey == null)
        {
            return;
        }

        WriteDwordIfMissing(localKey, "CustomSettings", SettingsDefaults.DefaultCustomSettings);
        WriteStringIfMissing(localKey, "ClassificationSelection", string.Empty);
        WriteStringIfMissing(localKey, "CustomBackgroundColor", SettingsDefaults.DefaultCustomBackgroundColor);
        WriteStringIfMissing(localKey, "CustomForeColor", SettingsDefaults.DefaultCustomForeColor);
        WriteStringIfMissing(localKey, "CustomDisplayText", string.Empty);
        WriteDwordIfMissing(localKey, "InfoCon", SettingsDefaults.DefaultInfoCon);
        WriteDwordIfMissing(localKey, "FpCon", SettingsDefaults.DefaultFpCon);
        WriteDwordIfMissing(localKey, "CaveatsEnabled", SettingsDefaults.DefaultCaveatsEnabled);
        WriteStringIfMissing(localKey, "Caveats", string.Empty);
        WriteDwordIfMissing(localKey, "BannerSize", SettingsDefaults.DefaultBannerSize);
        WriteDwordIfMissing(localKey, "DisableBorders", SettingsDefaults.DefaultDisableBorders);
        WriteDwordIfMissing(localKey, "ShowHostInformation", SettingsDefaults.DefaultShowHostInformation);
        WriteDwordIfMissing(localKey, "EnableBottomBanner", SettingsDefaults.DefaultEnableBottomBanner);
    }

    private static void WriteDwordIfMissing(RegistryKey key, string valueName, int defaultValue)
    {
        if (key.GetValue(valueName) == null)
        {
            key.SetValue(valueName, defaultValue, RegistryValueKind.DWord);
        }
    }

    private static void WriteStringIfMissing(RegistryKey key, string valueName, string defaultValue)
    {
        if (key.GetValue(valueName) == null)
        {
            key.SetValue(valueName, defaultValue, RegistryValueKind.String);
        }
    }

    internal string ResolveString(string policyPath, string localPath, string valueName, string compiledDefault)
    {
        var policyValue = ReadString(policyPath, valueName);
        if (!string.IsNullOrWhiteSpace(policyValue))
        {
            return policyValue;
        }

        var localValue = ReadString(localPath, valueName);
        return string.IsNullOrWhiteSpace(localValue) ? compiledDefault : localValue;
    }

    internal int ResolveInt(string policyPath, string localPath, string valueName, int compiledDefault)
    {
        if (TryReadInt(policyPath, valueName, out var policyInt))
        {
            return policyInt;
        }

        return TryReadInt(localPath, valueName, out var localInt) ? localInt : compiledDefault;
    }

    private string? ReadString(string path, string valueName)
    {
        using var key = _localMachineKey.OpenSubKey(path, false);
        return key?.GetValue(valueName)?.ToString();
    }

    private bool TryReadInt(string path, string valueName, out int value)
    {
        using var key = _localMachineKey.OpenSubKey(path, false);
        return TryConvertToInt(key?.GetValue(valueName), out value);
    }

    internal static bool TryConvertToInt(object? value, out int converted)
    {
        try
        {
            if (value == null)
            {
                converted = 0;
                return false;
            }

            converted = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception)
        {
            converted = 0;
            return false;
        }
    }
}

using System.Globalization;
using System.Collections.Generic;
using System;
using System.Linq;
using Microsoft.Win32;

namespace NetBannerNG.Utils
{
    public static class SettingsHelper
    {
        private const string LocalRegistryPath = @"SOFTWARE\NetBannerNG";
        private const string PolicyRegistryPath = @"SOFTWARE\Policies\Microsoft\NetBanner";
        private const int DefaultClassificationValue = 1;
        private const int DefaultFontSize = 9;
        private const int DefaultBannerSize = 20;
        private const int DefaultHeartbeat = 20;

        private static readonly string[] ManagedPolicyKeys =
        {
            "Classification",
            "CustomSettings",
            "CustomBackgroundColor",
            "CustomForeColor",
            "CustomDisplayText",
            "InfoCon",
            "FpCon",
            "CaveatsEnabled",
            "Caveats",
            "FontSize",
            "BannerSize",
            "Heartbeat",
            "DisableBorders",
        };

        internal static SettingsSnapshot LoadSettings()
        {
            using var localMachineKey = OpenLocalMachineKey();
            using var localKey = OpenLocalSettingsKey(localMachineKey);
            var localDefaults = LoadOrCreateLocalSettings(localKey);
            using var policyKey = localMachineKey.OpenSubKey(PolicyRegistryPath, false);

            return policyKey != null && HasManagedPolicyValue(policyKey)
                ? LoadPolicySettings(policyKey, localDefaults)
                : localDefaults;
        }

        private static SettingsSnapshot LoadOrCreateLocalSettings(RegistryKey localKey) => new()
        {
            Classification = GetOrCreateString(localKey, "Classification", MapClassification(DefaultClassificationValue)),
            CustomBackgroundColor = GetOrCreateEnumName(localKey, "CustomBackgroundColor", CustomBackgroundColors.Green),
            CustomForeColor = GetOrCreateEnumName(localKey, "CustomForeColor", CustomForeColors.White),
            FontSize = GetOrCreateInt(localKey, "FontSize", DefaultFontSize),
            BannerSize = GetOrCreateInt(localKey, "BannerSize", DefaultBannerSize),
            Heartbeat = GetOrCreateInt(localKey, "Heartbeat", DefaultHeartbeat),
            DisableBorders = GetOrCreateBool(localKey, "DisableBorders", false),
        };

        private static SettingsSnapshot LoadPolicySettings(RegistryKey policyKey, SettingsSnapshot localDefaults)
        {
            var defaultClassification = ParseClassification(localDefaults.Classification);
            var classification = GetInt(policyKey, "Classification", defaultClassification);
            var customSettings = GetInt(policyKey, "CustomSettings", 0) == 1;
            var caveatsEnabled = GetInt(policyKey, "CaveatsEnabled", 0) == 1;

            var background = GetEnum(policyKey, "CustomBackgroundColor", CustomBackgroundColors.Green);
            var foreground = GetEnum(policyKey, "CustomForeColor", CustomForeColors.White);

            var displayText = customSettings ? GetString(policyKey, "CustomDisplayText", string.Empty) : string.Empty;
            var infocon = GetInt(policyKey, "InfoCon", 0);
            var fpcon = GetInt(policyKey, "FpCon", 0);
            var caveats = caveatsEnabled ? GetString(policyKey, "Caveats", string.Empty) : string.Empty;

            return new SettingsSnapshot
            {
                Classification = ComposeClassificationText(MapClassification(classification), displayText, infocon, fpcon, caveats),
                CustomBackgroundColor = customSettings ? background.ToString() : localDefaults.CustomBackgroundColor,
                CustomForeColor = customSettings ? foreground.ToString() : localDefaults.CustomForeColor,
                FontSize = GetInt(policyKey, "FontSize", localDefaults.FontSize),
                BannerSize = GetInt(policyKey, "BannerSize", localDefaults.BannerSize),
                Heartbeat = GetInt(policyKey, "Heartbeat", localDefaults.Heartbeat),
                DisableBorders = GetBool(policyKey, "DisableBorders", localDefaults.DisableBorders),
            };
        }

        private static string ComposeClassificationText(string classification, string customDisplayText, int infoCon, int fpCon, string caveats)
        {
            var values = new List<string> { classification };
            if (!string.IsNullOrWhiteSpace(customDisplayText))
            {
                values.Add(customDisplayText.Trim());
            }

            if (infoCon > 0)
            {
                values.Add($"INFOCON {infoCon}");
            }

            if (fpCon > 0)
            {
                values.Add($"FPCON {fpCon}");
            }

            if (!string.IsNullOrWhiteSpace(caveats))
            {
                values.Add(caveats.Trim());
            }

            return string.Join(" | ", values);
        }

        private static string MapClassification(int value) => value switch
        {
            1 => "Unclassified",
            2 => "Secret",
            3 => "Top Secret",
            4 => "SCI",
            _ => "Public",
        };

        private static int ParseClassification(string classification) => classification switch
        {
            "Unclassified" => 1,
            "Secret" => 2,
            "Top Secret" => 3,
            "SCI" => 4,
            _ => DefaultClassificationValue,
        };

        private static bool HasManagedPolicyValue(RegistryKey policyKey)
            => ManagedPolicyKeys.Any(key => policyKey.GetValue(key) != null);

        private static TEnum GetEnum<TEnum>(RegistryKey key, string name, TEnum defaultValue) where TEnum : struct, Enum
        {
            var rawValue = GetInt(key, name, Convert.ToInt32(defaultValue, CultureInfo.InvariantCulture));
            return Enum.IsDefined(typeof(TEnum), rawValue)
                ? (TEnum)Enum.ToObject(typeof(TEnum), rawValue)
                : defaultValue;
        }

        private static string GetString(RegistryKey key, string name, string defaultValue) => key?.GetValue(name)?.ToString() ?? defaultValue;

        private static string GetOrCreateString(RegistryKey key, string name, string defaultValue)
        {
            var value = key.GetValue(name)?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            key.SetValue(name, defaultValue, RegistryValueKind.String);
            return defaultValue;
        }

        private static int GetOrCreateInt(RegistryKey key, string name, int defaultValue)
        {
            var value = key.GetValue(name);
            if (value != null)
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            key.SetValue(name, defaultValue, RegistryValueKind.DWord);
            return defaultValue;
        }

        private static string GetOrCreateEnumName<TEnum>(RegistryKey key, string name, TEnum defaultValue) where TEnum : struct, Enum
        {
            var raw = key.GetValue(name);
            if (raw != null)
            {
                return ParseEnumValue(raw, defaultValue).ToString();
            }

            key.SetValue(name, Convert.ToInt32(defaultValue, CultureInfo.InvariantCulture), RegistryValueKind.DWord);
            return defaultValue.ToString();
        }

        private static TEnum ParseEnumValue<TEnum>(object rawValue, TEnum defaultValue) where TEnum : struct, Enum
        {
            if (rawValue is string text)
            {
                if (Enum.TryParse<TEnum>(text, true, out var parsedByName) && Enum.IsDefined(typeof(TEnum), parsedByName))
                {
                    return parsedByName;
                }

                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedByNumber)
                    && Enum.IsDefined(typeof(TEnum), parsedByNumber)
                    ? (TEnum)Enum.ToObject(typeof(TEnum), parsedByNumber)
                    : defaultValue;
            }

            var asInt = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
            return Enum.IsDefined(typeof(TEnum), asInt)
                ? (TEnum)Enum.ToObject(typeof(TEnum), asInt)
                : defaultValue;
        }

        private static bool GetOrCreateBool(RegistryKey key, string name, bool defaultValue)
        {
            var value = key.GetValue(name);
            if (value != null)
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
            }

            key.SetValue(name, defaultValue ? 1 : 0, RegistryValueKind.DWord);
            return defaultValue;
        }

        private static RegistryKey OpenLocalMachineKey()
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        }

        private static RegistryKey OpenLocalSettingsKey(RegistryKey localMachineKey)
            => localMachineKey.CreateSubKey(LocalRegistryPath, true)
                ?? throw new InvalidOperationException($@"Unable to open or create HKLM\{LocalRegistryPath}");

        private static int GetInt(RegistryKey key, string name, int defaultValue)
        {
            var value = key?.GetValue(name);
            return value == null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static bool GetBool(RegistryKey key, string name, bool defaultValue)
        {
            var value = key?.GetValue(name);
            return value == null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
        }

        internal sealed class SettingsSnapshot
        {
            internal string Classification { get; set; }
            internal string CustomBackgroundColor { get; set; }
            internal string CustomForeColor { get; set; }
            internal int FontSize { get; set; }
            internal int BannerSize { get; set; }
            internal int Heartbeat { get; set; }
            internal bool DisableBorders { get; set; }
        }
    }
}

using Microsoft.Win32;
using System.Globalization;

namespace NetBannerNG.Utils
{
    internal static class SettingsHelper
    {
        private const string LocalRegistryPath = @"SOFTWARE\NetBannerNG";
        private const string PolicyRegistryPath = @"SOFTWARE\Policies\Microsoft\NetBanner";
        private const int DefaultClassificationValue = 1;

        internal static GeneralSettings LoadGeneralSettings(this Settings settings)
        {
            using var policyKey = Registry.LocalMachine.OpenSubKey(PolicyRegistryPath, false);
            if (policyKey != null && policyKey.GetValue("Classification") != null)
            {
                return LoadPolicySettings(policyKey);
            }

            using var localKey = OpenLocalSettingsKey();
            return new GeneralSettings
            {
                Classification = GetString(localKey, "Classification", MapClassification(DefaultClassificationValue)),
                BannerColor = GetString(localKey, "BannerColor", CustomBackgroundColors.Green.ToString()),
                FontColor = GetString(localKey, "FontColor", CustomForeColors.White.ToString()),
                FontSize = GetInt(localKey, "FontSize", 9),
                BannerSize = GetInt(localKey, "BannerSize", 20),
                Heartbeat = GetInt(localKey, "Heartbeat", 20),
                DisableBorders = GetBool(localKey, "DisableBorders", false),
                IsPolicyManaged = false,
            };
        }

        internal static void SaveSettings(this Settings settings, GeneralSettings generalSettings)
        {
            using var key = OpenLocalSettingsKey();
            key?.SetValue("Classification", generalSettings.Classification ?? MapClassification(DefaultClassificationValue), RegistryValueKind.String);
            key?.SetValue("BannerColor", generalSettings.BannerColor ?? CustomBackgroundColors.Green.ToString(), RegistryValueKind.String);
            key?.SetValue("FontColor", generalSettings.FontColor ?? CustomForeColors.White.ToString(), RegistryValueKind.String);
            key?.SetValue("FontSize", generalSettings.FontSize, RegistryValueKind.DWord);
            key?.SetValue("BannerSize", generalSettings.BannerSize, RegistryValueKind.DWord);
            key?.SetValue("Heartbeat", generalSettings.Heartbeat, RegistryValueKind.DWord);
            key?.SetValue("DisableBorders", generalSettings.DisableBorders ? 1 : 0, RegistryValueKind.DWord);
        }

        private static GeneralSettings LoadPolicySettings(RegistryKey policyKey)
        {
            var classification = GetInt(policyKey, "Classification", DefaultClassificationValue);
            var customSettings = GetInt(policyKey, "CustomSettings", 0) == 1;
            var caveatsEnabled = GetInt(policyKey, "CaveatsEnabled", 0) == 1;

            var background = GetEnum(policyKey, "CustomBackgroundColor", CustomBackgroundColors.Green);
            var foreground = GetEnum(policyKey, "CustomForeColor", CustomForeColors.White);

            var displayText = customSettings ? GetString(policyKey, "CustomDisplayText", string.Empty) : string.Empty;
            var infocon = GetInt(policyKey, "InfoCon", 0);
            var fpcon = GetInt(policyKey, "FpCon", 0);
            var caveats = caveatsEnabled ? GetString(policyKey, "Caveats", string.Empty) : string.Empty;

            return new GeneralSettings
            {
                Classification = ComposeClassificationText(MapClassification(classification), displayText, infocon, fpcon, caveats),
                BannerColor = customSettings ? background.ToString() : CustomBackgroundColors.Green.ToString(),
                FontColor = customSettings ? foreground.ToString() : CustomForeColors.White.ToString(),
                FontSize = 9,
                BannerSize = 20,
                Heartbeat = 20,
                DisableBorders = false,
                InfoCon = infocon,
                FpCon = fpcon,
                Caveats = caveats,
                CustomDisplayText = displayText,
                IsPolicyManaged = true,
            };
        }

        private static string ComposeClassificationText(string classification, string customDisplayText, int infoCon, int fpCon, string caveats)
        {
            var values = new List<string> { classification };
            if (!string.IsNullOrWhiteSpace(customDisplayText)) values.Add(customDisplayText.Trim());
            if (infoCon > 0) values.Add($"INFOCON {infoCon}");
            if (fpCon > 0) values.Add($"FPCON {fpCon}");
            if (!string.IsNullOrWhiteSpace(caveats)) values.Add(caveats.Trim());
            return string.Join(" | ", values);
        }

        private static string MapClassification(int value) => value switch
        {
            1 => "Unclassified",
            2 => "Confidential",
            3 => "Secret",
            4 => "Top Secret",
            _ => "Public",
        };

        private static TEnum GetEnum<TEnum>(RegistryKey key, string name, TEnum defaultValue) where TEnum : struct, Enum
        {
            var rawValue = GetInt(key, name, Convert.ToInt32(defaultValue, CultureInfo.InvariantCulture));
            return Enum.IsDefined(typeof(TEnum), rawValue)
                ? (TEnum)Enum.ToObject(typeof(TEnum), rawValue)
                : defaultValue;
        }

        private static string GetString(RegistryKey key, string name, string defaultValue) => key?.GetValue(name)?.ToString() ?? defaultValue;

        private static RegistryKey OpenLocalSettingsKey() => Registry.LocalMachine.CreateSubKey(LocalRegistryPath, true);

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
    }
}

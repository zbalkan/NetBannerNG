using Microsoft.Win32;
using System.Globalization;

namespace NetBannerNG.Utils
{
    internal static class SettingsHelper
    {
        private const string RegistryPath = @"SOFTWARE\NetBannerNG";

        internal static GeneralSettings LoadGeneralSettings(this Settings settings)
        {
            using var key = OpenSettingsKey();
            return new GeneralSettings
            {
                Classification = GetString(key, "Classification", "Public"),
                BannerColor = GetString(key, "BannerColor", "Green"),
                FontColor = GetString(key, "FontColor", "White"),
                FontSize = GetInt(key, "FontSize", 9),
                BannerSize = GetInt(key, "BannerSize", 20),
                Heartbeat = GetInt(key, "Heartbeat", 20),
                DisableBorders = GetBool(key, "DisableBorders", false),
            };
        }

        internal static void SaveSettings(this Settings settings, GeneralSettings generalSettings)
        {
            using var key = OpenSettingsKey();
            key?.SetValue("Classification", generalSettings.Classification ?? "Public", RegistryValueKind.String);
            key?.SetValue("BannerColor", generalSettings.BannerColor ?? "Green", RegistryValueKind.String);
            key?.SetValue("FontColor", generalSettings.FontColor ?? "White", RegistryValueKind.String);
            key?.SetValue("FontSize", generalSettings.FontSize, RegistryValueKind.DWord);
            key?.SetValue("BannerSize", generalSettings.BannerSize, RegistryValueKind.DWord);
            key?.SetValue("Heartbeat", generalSettings.Heartbeat, RegistryValueKind.DWord);
            key?.SetValue("DisableBorders", generalSettings.DisableBorders ? 1 : 0, RegistryValueKind.DWord);
        }

        private static string GetString(RegistryKey key, string name, string defaultValue) => key?.GetValue(name)?.ToString() ?? defaultValue;

        private static RegistryKey OpenSettingsKey() => Registry.LocalMachine.CreateSubKey(RegistryPath, true);

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

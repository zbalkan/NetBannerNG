using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using Microsoft.Win32;
using NetBannerNG.Classification;
using NetBannerNG.Utils;

namespace NetBannerNG
{
    internal sealed class Settings
    {
        private const double BorderBannerRatio = 0.15;
        private const int DefaultBannerSize = SettingsDefaults.DefaultBannerSize;
        private const string DefaultClassificationProfile = SettingsDefaults.DefaultClassificationProfile;
        private const int MinimumBorderSize = 2;
        private const string PolicyRegistryPath = SettingsDefaults.PolicyRegistryPath;
        private static readonly BrushConverter BrushConverter = new();
        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private static readonly string[] ManagedPolicyKeys =
                {
            "ClassificationSelection", "CustomSettings", "CustomBackgroundColor", "CustomForeColor", "CustomDisplayText",
            "InfoCon", "FpCon", "CaveatsEnabled", "Caveats", "BannerSize", "DisableBorders", "ShowHostInformation", "EnableBottomBanner",
        };

        private SettingsSnapshot? _currentSettings;
        private bool _needsResize;

        private Settings()
        {
            Refresh();
        }

        public int BannerSize { get; private set; }
        public string CaveatsText { get; private set; } = string.Empty;
        public string? Classification { get; private set; }
        public string ConditionMetadata { get; private set; } = string.Empty;
        public SolidColorBrush? CustomBackgroundColor { get; private set; }
        public SolidColorBrush? CustomForeColor { get; private set; }
        public bool DisableBorders { get; private set; }
        public bool ShowHostInformation { get; private set; }
        public bool EnableBottomBanner { get; private set; }
        public string? HostInformation { get; private set; }
        internal static Settings Instance => Lazy.Value;
        internal int BorderSize => Math.Max(MinimumBorderSize, (int)(BannerSize * BorderBannerRatio));
        internal bool NeedsResize => _needsResize;

        internal void Refresh()
        {
            var newSettings = LoadSettings();
            _needsResize = _currentSettings == null || newSettings.DisableBorders != _currentSettings.DisableBorders || newSettings.BannerSize != _currentSettings.BannerSize;

            Classification = newSettings.Classification ?? string.Empty;
            CustomBackgroundColor = ParseBackgroundBrush(newSettings.CustomBackgroundColor!);
            CustomForeColor = ParseForegroundBrush(newSettings.CustomForeColor!);
            BannerSize = newSettings.BannerSize;
            DisableBorders = newSettings.DisableBorders;
            ShowHostInformation = newSettings.ShowHostInformation;
            EnableBottomBanner = newSettings.EnableBottomBanner;
            CaveatsText = newSettings.Caveats ?? string.Empty;
            ConditionMetadata = BuildConditionMetadata(newSettings.InfoCon, newSettings.FpCon);
            HostInformation = ShowHostInformation ? GatherHostInfo() : string.Empty;

            _currentSettings = newSettings;
        }

        private static string BuildConditionMetadata(int infoCon, int fpCon)
        {
            var values = new List<string>();
            AppendConditionValues(values, infoCon, fpCon);
            return string.Join(" | ", values);
        }

        private static string ComposeClassificationText(string classification, string customDisplayText, int infoCon, int fpCon, string caveats)
        {
            var values = new List<string> { classification };
            if (!string.IsNullOrWhiteSpace(customDisplayText))
            {
                values.Add(customDisplayText.Trim());
            }

            AppendConditionValues(values, infoCon, fpCon);

            if (!string.IsNullOrWhiteSpace(caveats))
            {
                values.Add(caveats.Trim());
            }

            return string.Join(" | ", values);
        }

        private static string GatherHostInfo() => $"{Environment.MachineName} | {Environment.UserName} | {NetworkHelper.GetPhysicalIPAddress()}";

        private static bool GetBool(RegistryKey key, string name, bool defaultValue)
        {
            var value = key?.GetValue(name);
            return value == null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
        }

        private static int GetInt(RegistryKey key, string name, int defaultValue)
        {
            var value = key?.GetValue(name);
            return value == null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string GetString(RegistryKey key, string name, string defaultValue) => key?.GetValue(name)?.ToString() ?? defaultValue;

        private static SettingsSnapshot LoadSettings()
        {
            using var localMachineKey = OpenLocalMachineKey();
            using var policyKey = ResolvePolicyKey(localMachineKey);

            if (policyKey == null || !HasManagedPolicyValues(policyKey))
            {
                return BuildDefaultSettingsSnapshot();
            }

            var selection = GetString(policyKey!, "ClassificationSelection", $"NOT CONFIGURED - {ClassificationCatalogRegistry.NotConfiguredLabelText}");
            var (classificationProfile, classificationLabel) = ParseClassificationSelection(selection);
            var customSettings = GetInt(policyKey!, "CustomSettings", 0) == 1;
            var caveatsEnabled = GetInt(policyKey!, "CaveatsEnabled", 0) == 1;
            var displayText = customSettings ? GetString(policyKey!, "CustomDisplayText", string.Empty) : string.Empty;
            var infocon = GetInt(policyKey!, "InfoCon", 0);
            var fpcon = GetInt(policyKey!, "FpCon", 0);
            var caveats = caveatsEnabled ? GetString(policyKey!, "Caveats", string.Empty) : string.Empty;
            var classificationText = ComposeClassificationText(classificationLabel, displayText, infocon, fpcon, caveats);

            var policyBackground = GetString(policyKey!, "CustomBackgroundColor", "#FFFFFF");
            var policyForeground = GetString(policyKey!, "CustomForeColor", "#000000");
            return new SettingsSnapshot
            {
                Classification = classificationText,
                Caveats = caveats,
                InfoCon = infocon,
                FpCon = fpcon,
                CustomBackgroundColor = customSettings ? policyBackground : ResolveCatalogBackground(classificationProfile, classificationText),
                CustomForeColor = customSettings ? policyForeground : ResolveCatalogForeground(classificationProfile, classificationText),
                BannerSize = GetInt(policyKey!, "BannerSize", DefaultBannerSize),
                DisableBorders = GetBool(policyKey!, "DisableBorders", SettingsDefaults.DefaultDisableBorders == 1),
                ShowHostInformation = GetBool(policyKey!, "ShowHostInformation", false),
                EnableBottomBanner = GetBool(policyKey!, "EnableBottomBanner", SettingsDefaults.DefaultEnableBottomBanner == 1),
            };
        }

        private static SettingsSnapshot BuildDefaultSettingsSnapshot() => new()
        {
            Classification = ClassificationCatalogRegistry.NotConfiguredLabelText,
            Caveats = string.Empty,
            InfoCon = SettingsDefaults.DefaultInfoCon,
            FpCon = SettingsDefaults.DefaultFpCon,
            CustomBackgroundColor = NormalizeColorValue(SettingsDefaults.DefaultCustomBackgroundColor),
            CustomForeColor = NormalizeColorValue(SettingsDefaults.DefaultCustomForeColor),
            BannerSize = DefaultBannerSize,
            DisableBorders = SettingsDefaults.DefaultDisableBorders == 1,
            ShowHostInformation = SettingsDefaults.DefaultShowHostInformation == 1,
            EnableBottomBanner = SettingsDefaults.DefaultEnableBottomBanner == 1,
        };

        private static RegistryKey? ResolvePolicyKey(RegistryKey localMachineKey)
        {
            using var writablePolicyKey = localMachineKey.CreateSubKey(PolicyRegistryPath, true);
            if (writablePolicyKey == null)
            {
                return null;
            }

            DeleteUnusedValues(writablePolicyKey);
            WriteDefaultPolicyValues(writablePolicyKey);
            return localMachineKey.OpenSubKey(PolicyRegistryPath, false);
        }

        private static void DeleteUnusedValues(RegistryKey key)
        {
            key.DeleteValue("CpCon", false);
            key.DeleteValue("Heartbeat", false);
        }

        private static void WriteDefaultPolicyValues(RegistryKey policyKey)
        {
            WritePolicyStringIfMissing(policyKey, "ClassificationSelection", $"NOT CONFIGURED - {ClassificationCatalogRegistry.NotConfiguredLabelText}");
            WritePolicyDwordIfMissing(policyKey, "CustomSettings", SettingsDefaults.DefaultCustomSettings);
            WritePolicyStringIfMissing(policyKey, "CustomBackgroundColor", SettingsDefaults.DefaultCustomBackgroundColor);
            WritePolicyStringIfMissing(policyKey, "CustomForeColor", SettingsDefaults.DefaultCustomForeColor);
            WritePolicyStringIfMissing(policyKey, "CustomDisplayText", string.Empty);
            WritePolicyDwordIfMissing(policyKey, "InfoCon", SettingsDefaults.DefaultInfoCon);
            WritePolicyDwordIfMissing(policyKey, "FpCon", SettingsDefaults.DefaultFpCon);
            WritePolicyDwordIfMissing(policyKey, "CaveatsEnabled", SettingsDefaults.DefaultCaveatsEnabled);
            WritePolicyStringIfMissing(policyKey, "Caveats", string.Empty);
            WritePolicyDwordIfMissing(policyKey, "BannerSize", SettingsDefaults.DefaultBannerSize);
            WritePolicyDwordIfMissing(policyKey, "DisableBorders", SettingsDefaults.DefaultDisableBorders);
            WritePolicyDwordIfMissing(policyKey, "ShowHostInformation", SettingsDefaults.DefaultShowHostInformation);
            WritePolicyDwordIfMissing(policyKey, "EnableBottomBanner", SettingsDefaults.DefaultEnableBottomBanner);
        }

        private static void WritePolicyDwordIfMissing(RegistryKey policyKey, string valueName, int defaultValue)
        {
            if (policyKey.GetValue(valueName) == null)
            {
                policyKey.SetValue(valueName, defaultValue, RegistryValueKind.DWord);
            }
        }

        private static void WritePolicyStringIfMissing(RegistryKey policyKey, string valueName, string defaultValue)
        {
            if (policyKey.GetValue(valueName) == null)
            {
                policyKey.SetValue(valueName, defaultValue, RegistryValueKind.String);
            }
        }

        private static (string ProfileName, string ClassificationLabel) ParseClassificationSelection(string rawSelection)
        {
            var selection = (rawSelection ?? string.Empty).Trim();
            var separatorIndex = selection.IndexOf(" - ", StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex >= selection.Length - 3)
            {
                return (DefaultClassificationProfile, ClassificationCatalogRegistry.NotConfiguredLabelText);
            }

            var profile = selection.Substring(0, separatorIndex).Trim().ToUpperInvariant().Replace(' ', '_');
            var label = selection.Substring(separatorIndex + 3).Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                label = ClassificationCatalogRegistry.NotConfiguredLabelText;
            }

            return (profile, label);
        }

        private static bool HasManagedPolicyValues(RegistryKey? policyKey)
        {
            if (policyKey == null)
            {
                return false;
            }

            foreach (var key in ManagedPolicyKeys)
            {
                if (policyKey.GetValue(key) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendConditionValues(List<string> values, int infoCon, int fpCon)
        {
            AddConditionIfEnabled(values, "INFOCON", infoCon);
            AddConditionIfEnabled(values, "FPCON", fpCon);
        }

        private static void AddConditionIfEnabled(List<string> values, string label, int level)
        {
            if (level > 0)
            {
                values.Add($"{label} {level}");
            }
        }

        private static string NormalizeColorValue(string raw)
                    => string.IsNullOrWhiteSpace(raw) ? "#FFFFFF" : raw.Trim();

        private static RegistryKey OpenLocalMachineKey()
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        }

        private static SolidColorBrush ParseBackgroundBrush(string value) => ParseBrushWithFallback(value, "#FFFFFF");

        private static SolidColorBrush ParseBrushWithFallback(string value, string fallbackHex)
        {
            if (TryParseBrush(value, out var brush))
            {
                return brush;
            }

            return (SolidColorBrush)BrushConverter.ConvertFromInvariantString(fallbackHex);
        }

        private static SolidColorBrush ParseForegroundBrush(string value) => ParseBrushWithFallback(value, "#000000");

        private static string ResolveCatalogBackground(string profileName, string classificationText)
            => ClassificationCatalogRegistry.Resolve(profileName).ResolveBackgroundFromBannerText(classificationText, ClassificationCatalogRegistry.NotConfiguredBackgroundHex);

        private static string ResolveCatalogForeground(string profileName, string classificationText)
            => ClassificationCatalogRegistry.Resolve(profileName).ResolveForegroundFromBannerText(classificationText, ClassificationCatalogRegistry.NotConfiguredForegroundHex);

        private static bool TryParseBrush(string value, out SolidColorBrush brush)
        {
            try { brush = (SolidColorBrush)BrushConverter.ConvertFromInvariantString(value); return true; }
            catch (FormatException) { brush = new SolidColorBrush(); return false; }
        }

        internal sealed class SettingsSnapshot
        {
            internal int BannerSize { get; set; }
            internal string? Caveats { get; set; }
            internal string? Classification { get; set; }
            internal string? CustomBackgroundColor { get; set; }
            internal string? CustomForeColor { get; set; }
            internal bool DisableBorders { get; set; }
            internal int FpCon { get; set; }
            internal int InfoCon { get; set; }
            internal bool ShowHostInformation { get; set; }
            internal bool EnableBottomBanner { get; set; }
        }
    }
}

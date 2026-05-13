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
        private const int DefaultBannerSize = 28;
        private const int DefaultClassificationValue = 1;
        private const string DefaultClassificationProfile = "NATO";
        private const string LocalRegistryPath = @"SOFTWARE\NetBannerNG";
        private const int MinimumBorderSize = 2;
        private const string PolicyRegistryPath = @"SOFTWARE\Policies\NetbannerNG";
        private const string LegacyPolicyRegistryPath = @"SOFTWARE\Policies\Microsoft\NetBanner";
        private static readonly BrushConverter BrushConverter = new();
        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

        private static readonly string[] ManagedPolicyKeys =
                {
            "Classification", "CustomSettings", "CustomBackgroundColor", "CustomForeColor", "CustomDisplayText",
            "InfoCon", "FpCon", "CaveatsEnabled", "Caveats", "BannerSize", "DisableBorders", "ClassificationProfile", "ShowHostInformation", "EnableBottomBanner",
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

            Classification = (newSettings.Classification ?? string.Empty).ToUpperInvariant();
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

        private static string GetOrCreateString(RegistryKey key, string name, string defaultValue)
        {
            var value = key.GetValue(name)?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                return value!;
            }

            key.SetValue(name, defaultValue, RegistryValueKind.String);
            return defaultValue;
        }

        private static string GetString(RegistryKey key, string name, string defaultValue) => key?.GetValue(name)?.ToString() ?? defaultValue;

        private static SettingsSnapshot LoadOrCreateLocalSettings(RegistryKey localKey) => new()
        {
            Classification = GetOrCreateString(localKey, "Classification", MapClassification(DefaultClassificationValue)),
            Caveats = string.Empty,
            InfoCon = 0,
            FpCon = 0,
            CustomBackgroundColor = NormalizeColorValue(
                GetOrCreateString(localKey, "CustomBackgroundColor", "#007A33"),
                ToBackgroundHex),
            CustomForeColor = NormalizeColorValue(
                GetOrCreateString(localKey, "CustomForeColor", "#FFFFFF"),
                ToForegroundHex),
            BannerSize = GetOrCreateInt(localKey, "BannerSize", DefaultBannerSize),
            DisableBorders = GetOrCreateBool(localKey, "DisableBorders", false),
            ShowHostInformation = GetOrCreateBool(localKey, "ShowHostInformation", true),
            EnableBottomBanner = GetOrCreateBool(localKey, "EnableBottomBanner", false),
        };

        private static SettingsSnapshot LoadSettings()
        {
            using var localMachineKey = OpenLocalMachineKey();
            using var localKey = OpenCurrentUserSettingsKey();
            var localDefaults = LoadOrCreateLocalSettings(localKey);
            using var policyKey = ResolvePolicyKey(localMachineKey);

            if (!HasManagedPolicyValues(policyKey))
            {
                return localDefaults;
            }

            var defaultClassification = ParseClassification(localDefaults.Classification ?? MapClassification(DefaultClassificationValue));
            var classification = GetInt(policyKey!, "Classification", defaultClassification);
            var classificationProfile = ResolveEffectiveClassificationProfile(policyKey!);
            var customSettings = GetInt(policyKey!, "CustomSettings", 0) == 1;
            var caveatsEnabled = GetInt(policyKey!, "CaveatsEnabled", 0) == 1;
            var displayText = customSettings ? GetString(policyKey!, "CustomDisplayText", string.Empty) : string.Empty;
            var infocon = GetInt(policyKey!, "InfoCon", 0);
            var fpcon = GetInt(policyKey!, "FpCon", 0);
            var cpcon = GetInt(policyKey!, "CpCon", 0);
            var caveats = caveatsEnabled ? GetString(policyKey!, "Caveats", string.Empty) : string.Empty;
            var classificationText = ComposeClassificationText(MapClassification(classification), displayText, infocon, fpcon, caveats);

            var policyBackground = GetInt(policyKey!, "CustomBackgroundColor", (int)CustomBackgroundColors.Green);
            var policyForeground = GetInt(policyKey!, "CustomForeColor", (int)CustomForeColors.White);
            return new SettingsSnapshot
            {
                Classification = classificationText,
                Caveats = caveats,
                InfoCon = infocon,
                FpCon = fpcon,
                CustomBackgroundColor = customSettings ? ToBackgroundHex(policyBackground) : ResolveCatalogBackground(classificationProfile, classificationText),
                CustomForeColor = customSettings ? ToForegroundHex(policyForeground) : localDefaults.CustomForeColor,
                BannerSize = GetInt(policyKey!, "BannerSize", localDefaults.BannerSize),
                DisableBorders = GetBool(policyKey!, "DisableBorders", localDefaults.DisableBorders),
                ShowHostInformation = GetBool(policyKey!, "ShowHostInformation", false),
                EnableBottomBanner = GetBool(policyKey!, "EnableBottomBanner", localDefaults.EnableBottomBanner),
            };
        }

        private static RegistryKey? ResolvePolicyKey(RegistryKey localMachineKey)
        {
            var netBannerNgPolicyKey = localMachineKey.OpenSubKey(PolicyRegistryPath, true);
            if (HasManagedPolicyValues(netBannerNgPolicyKey))
            {
                return netBannerNgPolicyKey;
            }

            using var legacyPolicyKey = localMachineKey.OpenSubKey(LegacyPolicyRegistryPath, false);
            if (!HasManagedPolicyValues(legacyPolicyKey))
            {
                return netBannerNgPolicyKey;
            }

            using (var writablePolicyKey = localMachineKey.CreateSubKey(PolicyRegistryPath, true))
            {
                if (writablePolicyKey == null)
                {
                    return netBannerNgPolicyKey;
                }

                CopyManagedPolicyValues(legacyPolicyKey!, writablePolicyKey);
            }

            netBannerNgPolicyKey?.Dispose();
            return localMachineKey.OpenSubKey(PolicyRegistryPath, false);
        }

        private static void CopyManagedPolicyValues(RegistryKey sourceKey, RegistryKey destinationKey)
        {
            foreach (var key in ManagedPolicyKeys)
            {
                var value = sourceKey.GetValue(key);
                if (value == null)
                {
                    continue;
                }

                var valueKind = sourceKey.GetValueKind(key);
                destinationKey.SetValue(key, value, valueKind);
            }
        }

        private static string MapClassification(int value) => value switch
        {
            10 => "CUI",
            1 => "UNCLASSIFIED",
            2 => "SECRET",
            3 => "TOP SECRET",
            4 => "SCI",
            5 => "PUBLIC",
            6 => "RESTRICTED",
            7 => "CONFIDENTIAL",
            8 => "SENSITIVE",
            9 => "FOR OFFICIAL USE ONLY",
            _ => ClassificationCatalogRegistry.Resolve(DefaultClassificationProfile).CanonicalLabelForValue(value, "PUBLIC"),
        };

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

        private static string ResolveEffectiveClassificationProfile(RegistryKey policyKey)
        {
            var configuredProfile = GetString(policyKey, "ClassificationProfile", string.Empty).Trim();
            if (!string.IsNullOrEmpty(configuredProfile))
            {
                return configuredProfile;
            }

            // Backward compatibility dispatch:
            // If legacy NetBanner-style classification values are configured and no explicit profile is set,
            // prefer US catalog behavior. Otherwise use NATO default.
            var classificationValue = GetInt(policyKey, "Classification", DefaultClassificationValue);
            return (classificationValue >= 1 && classificationValue <= 4) || classificationValue == 10 ? "US" : DefaultClassificationProfile;
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

        private static string NormalizeColorValue(string raw, Func<int, string> intMapper)
                    => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? intMapper(number)
                : raw;

        private static RegistryKey OpenLocalMachineKey()
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        }

        private static RegistryKey OpenCurrentUserSettingsKey()
            => Registry.CurrentUser.CreateSubKey(LocalRegistryPath, true) ?? throw new InvalidOperationException($@"Unable to open or create HKCU\{LocalRegistryPath}");

        private static SolidColorBrush ParseBackgroundBrush(string value) => ParseBrushWithFallback(value, ToBackgroundHex((int)CustomBackgroundColors.Green), ToBackgroundHex);

        private static SolidColorBrush ParseBrushWithFallback(string value, string fallbackHex, Func<int, string> fromInt)
        {
            if (TryParseBrush(value, out var brush))
            {
                return brush;
            }

            if (int.TryParse(value, out var colorInt) && TryParseBrush(fromInt(colorInt), out brush))
            {
                return brush;
            }

            return (SolidColorBrush)BrushConverter.ConvertFromInvariantString(fallbackHex);
        }

        private static int ParseClassification(string classification)
                    => classification.Trim().ToUpperInvariant() switch
                    {
                        "UNCLASSIFIED" => 1,
                        "SECRET" => 2,
                        "TOP SECRET" => 3,
                        "SCI" => 4,
                        _ => DefaultClassificationValue,
                    };

        private static SolidColorBrush ParseForegroundBrush(string value) => ParseBrushWithFallback(value, ToForegroundHex((int)CustomForeColors.White), ToForegroundHex);

        private static string ResolveCatalogBackground(string profileName, string classificationText)
            => ClassificationCatalogRegistry.Resolve(profileName).ResolveBackgroundFromBannerText(classificationText, "#007A33");

        private static string ToBackgroundHex(int value) => value switch
        {
            1 => "#007A33",
            2 => "#0033A0",
            3 => "#C8102E",
            4 => "#F7EA48",
            5 => "#FFFFFF",
            6 => "#000000",
            7 => "#8B4513",
            8 => "#800080",
            9 => "#FF671F",
            _ => "#007A33",
        };

        private static string ToForegroundHex(int value) => value switch
        {
            1 => "#000000",
            2 => "#FFFFFF",
            3 => "#C8102E",
            _ => "#FFFFFF",
        };

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

using System;
using System.Collections.Generic;
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
        private const int MinBannerSize = SettingsDefaults.MinBannerSize;
        private const int MaxBannerSize = SettingsDefaults.MaxBannerSize;
        private const int MaxCaveatsLength = SettingsDefaults.MaxCaveatsLength;
        private const int MinInfoCon = SettingsDefaults.MinInfoCon;
        private const int MaxInfoCon = SettingsDefaults.MaxInfoCon;
        private const int MinFpCon = SettingsDefaults.MinFpCon;
        private const int MaxFpCon = SettingsDefaults.MaxFpCon;
        private const string DefaultClassificationProfile = SettingsDefaults.DefaultClassificationProfile;
        private const string DefaultBackgroundHex = SettingsDefaults.DefaultCustomBackgroundColor;
        private const string DefaultForegroundHex = SettingsDefaults.DefaultCustomForeColor;
        private const int MinimumBorderSize = 2;
        private const string PolicyRegistryPath = SettingsDefaults.PolicyRegistryPath;
        private const string LocalRegistryPath = SettingsDefaults.LocalRegistryPath;
        private static readonly BrushConverter BrushConverter = new();
        private static readonly Lazy<Settings> Lazy = new(() => new Settings());

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

        private static SettingsSnapshot LoadSettings()
        {
            using var localMachineKey = OpenLocalMachineKey();
            var registryReader = new SettingsRegistryReader(localMachineKey);
            registryReader.EnsureDebugRegistryKeys(PolicyRegistryPath, LocalRegistryPath);

            var selection = registryReader.ResolveString(PolicyRegistryPath, LocalRegistryPath, "ClassificationSelection", string.Empty);
            var (classificationProfile, classificationLabel) = ParseClassificationSelection(selection);
            var customSettings = registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "CustomSettings", SettingsDefaults.DefaultCustomSettings) == 1;
            var caveatsEnabled = registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "CaveatsEnabled", SettingsDefaults.DefaultCaveatsEnabled) == 1;
            var displayText = customSettings ? registryReader.ResolveString(PolicyRegistryPath, LocalRegistryPath, "CustomDisplayText", string.Empty) : string.Empty;
            var infocon = Clamp(registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "InfoCon", SettingsDefaults.DefaultInfoCon), MinInfoCon, MaxInfoCon);
            var fpcon = Clamp(registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "FpCon", SettingsDefaults.DefaultFpCon), MinFpCon, MaxFpCon);
            var caveats = caveatsEnabled ? Truncate(registryReader.ResolveString(PolicyRegistryPath, LocalRegistryPath, "Caveats", string.Empty), MaxCaveatsLength) : string.Empty;
            var classificationText = ComposeClassificationText(classificationLabel, displayText, infocon, fpcon, caveats);

            var configuredBackground = registryReader.ResolveString(PolicyRegistryPath, LocalRegistryPath, "CustomBackgroundColor", DefaultBackgroundHex);
            var configuredForeground = registryReader.ResolveString(PolicyRegistryPath, LocalRegistryPath, "CustomForeColor", DefaultForegroundHex);
            return new SettingsSnapshot
            {
                Classification = classificationText,
                Caveats = caveats,
                InfoCon = infocon,
                FpCon = fpcon,
                CustomBackgroundColor = customSettings ? configuredBackground : ResolveCatalogBackground(classificationProfile, classificationText),
                CustomForeColor = customSettings ? configuredForeground : ResolveCatalogForeground(classificationProfile, classificationText),
                BannerSize = Clamp(registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "BannerSize", DefaultBannerSize), MinBannerSize, MaxBannerSize),
                DisableBorders = registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "DisableBorders", SettingsDefaults.DefaultDisableBorders) == 1,
                ShowHostInformation = registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "ShowHostInformation", SettingsDefaults.DefaultShowHostInformation) == 1,
                EnableBottomBanner = registryReader.ResolveInt(PolicyRegistryPath, LocalRegistryPath, "EnableBottomBanner", SettingsDefaults.DefaultEnableBottomBanner) == 1,
            };
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

        private static int Clamp(int value, int min, int max)
            => Math.Min(max, Math.Max(min, value));

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static RegistryKey OpenLocalMachineKey()
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        }

        private static SolidColorBrush ParseBackgroundBrush(string value) => ParseBrushWithFallback(value, DefaultBackgroundHex);

        private static SolidColorBrush ParseBrushWithFallback(string value, string fallbackHex)
        {
            if (TryParseBrush(value, out var brush))
            {
                return brush;
            }

            return (SolidColorBrush)BrushConverter.ConvertFromInvariantString(fallbackHex);
        }

        private static SolidColorBrush ParseForegroundBrush(string value) => ParseBrushWithFallback(value, DefaultForegroundHex);

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
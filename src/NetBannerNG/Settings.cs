using NetBannerNG.Utils;
using System.Windows.Media;

namespace NetBannerNG
{
    internal sealed class Settings
    {
        private static readonly BrushConverter BrushConverter = new();
        #region General Settings

        internal string Classification { get; set; }

        internal SolidColorBrush CustomBackgroundColor { get; set; }

        internal SolidColorBrush CustomForeColor { get; set; }

        internal int FontSize { get; set; }

        internal int BannerSize { get; set; }

        internal int Heartbeat { get; set; }

        internal bool DisableBorders { get; set; }

        #endregion General Settings

        internal string HostInformation { get; set; }

        internal int BorderSize => Math.Max(MinimumBorderSize, (int)(BannerSize * BorderBannerRatio));

        internal bool NeedsResize => _needsResize;
        internal static Settings Instance => Lazy.Value;

        private static readonly Lazy<Settings> Lazy = new(() => new Settings());
        private SettingsHelper.SettingsSnapshot _currentSettings;
        private bool _needsResize;
        private const int MinimumBorderSize = 2;
        private const double BorderBannerRatio = 0.25;

        private Settings()
        {
            Refresh();
        }

        internal void Refresh()
        {
            var newSettings = SettingsHelper.LoadSettings();
            _needsResize = _currentSettings == null ||
                           newSettings.DisableBorders != _currentSettings.DisableBorders ||
                           newSettings.BannerSize != _currentSettings.BannerSize ||
                           newSettings.FontSize != _currentSettings.FontSize;

            Classification = newSettings.Classification;
            CustomBackgroundColor = ParseBrush(newSettings.CustomBackgroundColor);
            FontSize = newSettings.FontSize;
            CustomForeColor = ParseBrush(newSettings.CustomForeColor);
            BannerSize = newSettings.BannerSize;
            Heartbeat = newSettings.Heartbeat;
            DisableBorders = newSettings.DisableBorders;

            _currentSettings = newSettings;

            HostInformation = GatherHostInfo();
        }

        private static SolidColorBrush ParseBrush(string name)
            => (SolidColorBrush)BrushConverter.ConvertFromInvariantString(name);

        private static string GatherHostInfo() => $"{Environment.MachineName} | {Environment.UserName} | {Environment.OSVersion} | {NetworkHelper.GetPhysicalIPAddress()}";
    }
}

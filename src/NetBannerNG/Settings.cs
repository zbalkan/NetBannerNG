using NetBannerNG.Utils;
using System.Windows.Media;

namespace NetBannerNG
{
    internal sealed class Settings
    {
        #region General Settings

        internal string Classification { get; set; }

        internal SolidColorBrush BannerColor { get; set; }

        internal SolidColorBrush FontColor { get; set; }

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
        private GeneralSettings _currentGeneralSettings;
        private bool _needsResize;
        private const int MinimumBorderSize = 2;
        private const double BorderBannerRatio = 0.25;

        private Settings() => Refresh();

        internal void Refresh()
        {
            var newGeneralSettings = this.LoadGeneralSettings();
            _needsResize = _currentGeneralSettings == null ||
                           newGeneralSettings.DisableBorders != _currentGeneralSettings.DisableBorders ||
                           newGeneralSettings.BannerSize != _currentGeneralSettings.BannerSize ||
                           newGeneralSettings.FontSize != _currentGeneralSettings.FontSize;

            Classification = newGeneralSettings.Classification;
            BannerColor = ColorHelper.GetColorBrush(newGeneralSettings.BannerColor);
            FontSize = newGeneralSettings.FontSize;
            FontColor = ColorHelper.GetColorBrush(newGeneralSettings.FontColor);
            BannerSize = newGeneralSettings.BannerSize;
            Heartbeat = newGeneralSettings.Heartbeat;
            DisableBorders = newGeneralSettings.DisableBorders;

            _currentGeneralSettings = newGeneralSettings;

            HostInformation = GatherHostInfo();
        }

        private static string GatherHostInfo() => $"{Environment.MachineName} | {Environment.UserName} | {Environment.OSVersion} | {NetworkHelper.GetPhysicalIPAddress()}";
    }
}
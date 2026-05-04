using NetBannerNG.Controls.ColorPicker;
using NetBannerNG.Controls.ColorPicker.Code;
using NetBannerNG.Utils;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static NetBannerNG.Common.AppBar.WPFUnitHelper;

namespace NetBannerNG.Windows
{
    /// <summary>
    ///     Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly int[] _allowedFontSizes =
        {
            8,
            9,
            10,
            11,
            12,
            14,
            16,
            20,
            24,
            28,
            32,
            36,
            40
        };

        private SolidColorBrush _bannerColor;
        private int _bannerSize;
        private bool _disableBorders;
        private SolidColorBrush _fontColor;
        private int _fontSize;
        private int _heartbeat;

        internal SettingsWindow()
        {
            InitializeComponent();
            _bannerColor = Settings.Instance.BannerColor;
            _bannerSize = Settings.Instance.BannerSize;
            _disableBorders = Settings.Instance.DisableBorders;
            _fontColor = Settings.Instance.FontColor;
            _fontSize = Settings.Instance.FontSize;
            _heartbeat = Settings.Instance.Heartbeat;
        }

        private void ReadSettings()
        {
            decBannerBorderSize.Value = _bannerSize;

            tbBannerColor.Text = ColorHelper.GetColorName(_bannerColor);

            cboFontSize.ItemsSource = _allowedFontSizes;
            cboFontSize.SelectedValue = _fontSize;

            tbFontColor.Text = ColorHelper.GetColorName(_fontColor);

            decHeartbeat.Value = _heartbeat;

            cbDisableBorders.IsChecked = _disableBorders;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReadSettings();
        }

        private void btSave_Click(object sender, RoutedEventArgs e)
        {
            var newSettings = new GeneralSettings
            {
                BannerSize = _bannerSize,
                BannerColor = ColorHelper.GetColorName(_bannerColor),
                FontSize = _fontSize,
                FontColor = ColorHelper.GetColorName(_fontColor),
                Heartbeat = _heartbeat,
                DisableBorders = _disableBorders
            };
            Settings.Instance.SaveSettings(newSettings);
            Settings.Instance.Refresh();
            _ = MessageBox.Show(this, "The changes will be applied after clicking OK.", "SETTINGS SAVED!");

            BorderManager.ResizeAllBorders();
            Close();
        }

        private void cboFontSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _fontSize = (int)cboFontSize.SelectedValue;
            var minimumBannerSize = (int)(double)Converter.ConvertFromString($"{_fontSize}pt") + 8; // 8 is the balancing value, 4px margin from top and bottom
            if (decBannerBorderSize.Value >= minimumBannerSize)
            {
                return;
            }

            decBannerBorderSize.MinValue = minimumBannerSize;
            decBannerBorderSize.Value = minimumBannerSize;
        }

        private void cbDisableBorders_Unchecked(object sender, RoutedEventArgs e)
        {
            _disableBorders = false;
        }

        private void cbDisableBorders_Checked(object sender, RoutedEventArgs e)
        {
            _disableBorders = true;
        }

        private void decBannerBorderSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            _bannerSize = (int)decBannerBorderSize.Value;
        }

        private void decHeartbeat_ValueChanged(object sender, RoutedPropertyChangedEventArgs<decimal> e)
        {
            _heartbeat = (int)decHeartbeat.Value;
        }

        private void tbBannerColor_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _bannerColor = PickColor(_bannerColor);
            tbBannerColor.Text = ColorHelper.GetColorName(_bannerColor);
        }

        private void tbFontColor_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _fontColor = PickColor(_fontColor);
            tbFontColor.Text = ColorHelper.GetColorName(_fontColor);
        }

        private static SolidColorBrush PickColor(SolidColorBrush currentColor)
        {
            _ = ColorPickerWindow.ShowDialog(out var selectedColor, ColorPickerDialogOptions.SimpleView);
            return new SolidColorBrush(selectedColor == default ? currentColor.Color : selectedColor);
        }

        private void btExport_Click(object sender, RoutedEventArgs e)
        {
            var profilePath = UserHelper.UserProfilePath;

            var dialog = new SaveFileDialog
            {
                InitialDirectory = Path.Combine(profilePath, "Desktop"),
                AddExtension = true,
                DefaultExt = ".json",
                CreatePrompt = true,
                FileName = "settings",
                Filter = "JSON file |*.json",
                Title = "Export settings"
            };
            var ok = dialog.ShowDialog();
            if (ok == null || !ok.HasValue || !ok.Value)
            {
                return;
            }

            try
            {
                var newSettings = new GeneralSettings
                {
                    Classification = Settings.Instance.Classification,
                    BannerSize = _bannerSize,
                    BannerColor = ColorHelper.GetColorName(_bannerColor),
                    FontSize = _fontSize,
                    FontColor = ColorHelper.GetColorName(_fontColor),
                    Heartbeat = _heartbeat,
                    DisableBorders = _disableBorders
                };
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict
                };
                var json = JsonSerializer.Serialize(newSettings, options);
                File.WriteAllText(dialog.FileName, json);
                _ = MessageBox.Show($"Settings are exported to address successfully:{Environment.NewLine}{dialog.FileName}");
            }
            catch (Exception)
            {
                _ = MessageBox.Show("An error occured while exporting the settings. Please ensure you have sufficien privileges.");
            }
        }
    }
}
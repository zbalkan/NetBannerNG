using NetBannerNG.Controls.ColorPicker.Code;
using System.Windows;
using System.Windows.Media;

namespace NetBannerNG.Controls.ColorPicker
{
    /// <summary>
    ///     Interaction logic for ColorPickerWindow.xaml
    /// </summary>
    public partial class ColorPickerWindow : Window
    {
        private readonly int widthMax = 574;
        private readonly int widthMin = 342;

        public ColorPickerWindow()
        {
            InitializeComponent();
        }

        protected bool SimpleMode { get; set; }

        public static bool ShowDialog(out Color color, ColorPickerDialogOptions flags = ColorPickerDialogOptions.None,
            ColorPickerControl.ColorPickerChangeHandler customPreviewEventHandler = null)
        {
            if ((flags & ColorPickerDialogOptions.LoadCustomPalette) == ColorPickerDialogOptions.LoadCustomPalette)
            {
                ColorPickerSettings.UsingCustomPalette = true;
            }

            var instance = new ColorPickerWindow();
            color = instance.ColorPicker.Color;

            if ((flags & ColorPickerDialogOptions.SimpleView) == ColorPickerDialogOptions.SimpleView)
            {
                instance.ToggleSimpleAdvancedView();
            }

            if (ColorPickerSettings.UsingCustomPalette)
            {
                instance.ColorPicker.LoadDefaultCustomPalette();
            }

            if (customPreviewEventHandler != null)
            {
                instance.ColorPicker.OnPickColor += customPreviewEventHandler;
            }

            var result = instance.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return false;
            }

            color = instance.ColorPicker.Color;
            return true;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Hide();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Hide();
        }

        private void MinMaxViewButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (SimpleMode)
            {
                SimpleMode = false;
                MinMaxViewButton.Content = "<< Simple";
                Width = widthMax;
            }
            else
            {
                SimpleMode = true;
                MinMaxViewButton.Content = "Advanced >>";
                Width = widthMin;
            }
        }

        public void ToggleSimpleAdvancedView()
        {
            if (SimpleMode)
            {
                SimpleMode = false;
                MinMaxViewButton.Content = "<< Simple";
                Width = widthMax;
            }
            else
            {
                SimpleMode = true;
                MinMaxViewButton.Content = "Advanced >>";
                Width = widthMin;
            }
        }
    }
}
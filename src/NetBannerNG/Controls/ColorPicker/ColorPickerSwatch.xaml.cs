using NetBannerNG.Controls.ColorPicker.Code;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NetBannerNG.Controls.ColorPicker
{
    /// <summary>
    ///     Interaction logic for ColorPickerSwatch.xaml
    /// </summary>
    public partial class ColorPickerSwatch : UserControl
    {
        public delegate void ColorSwatchPickHandler(Color color);

        public ColorPickerSwatch()
        {
            InitializeComponent();
        }

        public static ColorPickerControl ColorPickerControl { get; set; }

        public bool Editable { get; set; }
        public Color CurrentColor { get; set; } = Colors.White;

        public event ColorSwatchPickHandler OnPickColor;

        private void UIElement_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border border)
            {
                return;
            }

            if (Editable && Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                border.Background = new SolidColorBrush(CurrentColor);

                if (border.DataContext is ColorSwatchItem data)
                {
                    data.Color = CurrentColor;
                    data.HexString = CurrentColor.ToHexString();
                }

                ColorPickerControl?.CustomColorsChanged();
            }
            else
            {
                var color = border.Background as SolidColorBrush;
                OnPickColor?.Invoke(color.Color);
            }
        }

        internal List<ColorSwatchItem> GetColors()
        {
            //_ = SwatchListBox.Items;

            return SwatchListBox.ItemsSource is List<ColorSwatchItem> colors ? colors : new List<ColorSwatchItem>();
        }
    }
}
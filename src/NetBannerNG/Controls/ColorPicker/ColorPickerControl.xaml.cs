using NetBannerNG.Controls.ColorPicker.Code;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NetBannerNG.Controls.ColorPicker
{
    /// <summary>
    ///     Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public partial class ColorPickerControl : UserControl
    {
        internal static ColorPalette colorPalette;

        internal List<ColorSwatchItem> colorSwatch1 = new();

        internal List<ColorSwatchItem> colorSwatch2 = new();

        protected const int NumColorsFirstSwatch = 39;

        protected const int NumColorsSecondSwatch = 112;

        private Color _color = Colors.White;

        private bool _isSettingValues;

        public ColorPickerControl()
        {
            InitializeComponent();

            ColorPickerSwatch.ColorPickerControl = this;

            // Load from file if possible
            if (ColorPickerSettings.UsingCustomPalette && File.Exists(ColorPickerSettings.CustomPaletteFilename))
            {
                colorPalette = colorPalette.LoadFromXml(ColorPickerSettings.CustomPaletteFilename);
            }

            if (colorPalette == null)
            {
                colorPalette = new ColorPalette();
                colorPalette.InitializeDefaults();
            }

            colorSwatch1.AddRange(colorPalette.BuiltInColors.Take(NumColorsFirstSwatch).ToArray());

            colorSwatch2.AddRange(colorPalette.BuiltInColors.Skip(NumColorsFirstSwatch).Take(NumColorsSecondSwatch)
                .ToArray());

            Swatch1.SwatchListBox.ItemsSource = colorSwatch1;
            Swatch2.SwatchListBox.ItemsSource = colorSwatch2;

            if (ColorPickerSettings.UsingCustomPalette)
            {
                CustomColorSwatch.SwatchListBox.ItemsSource = colorPalette.CustomColors;
            }
            else
            {
                customColorsLabel.Visibility = Visibility.Collapsed;
                CustomColorSwatch.Visibility = Visibility.Collapsed;
            }

            RSlider.Slider.Maximum = 255;
            GSlider.Slider.Maximum = 255;
            BSlider.Slider.Maximum = 255;
            ASlider.Slider.Maximum = 255;
            HSlider.Slider.Maximum = 360;
            SSlider.Slider.Maximum = 1;
            LSlider.Slider.Maximum = 1;

            RSlider.Label.Content = "R";
            RSlider.Slider.TickFrequency = 1;
            RSlider.Slider.IsSnapToTickEnabled = true;
            GSlider.Label.Content = "G";
            GSlider.Slider.TickFrequency = 1;
            GSlider.Slider.IsSnapToTickEnabled = true;
            BSlider.Label.Content = "B";
            BSlider.Slider.TickFrequency = 1;
            BSlider.Slider.IsSnapToTickEnabled = true;

            ASlider.Label.Content = "A";
            ASlider.Slider.TickFrequency = 1;
            ASlider.Slider.IsSnapToTickEnabled = true;

            HSlider.Label.Content = "H";
            HSlider.Slider.TickFrequency = 1;
            HSlider.Slider.IsSnapToTickEnabled = true;
            SSlider.Label.Content = "S";
            //SSlider.Slider.TickFrequency = 1;
            //SSlider.Slider.IsSnapToTickEnabled = true;
            LSlider.Label.Content = "V";
            //LSlider.Slider.TickFrequency = 1;
            //LSlider.Slider.IsSnapToTickEnabled = true;

            Color = _color;
        }

        public delegate void ColorPickerChangeHandler(Color color);

        public event ColorPickerChangeHandler OnPickColor;

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;

                CustomColorSwatch.CurrentColor = value;

                _isSettingValues = true;

                RSlider.Slider.Value = _color.R;
                GSlider.Slider.Value = _color.G;
                BSlider.Slider.Value = _color.B;
                ASlider.Slider.Value = _color.A;

                SSlider.Slider.Value = _color.GetSaturation();
                LSlider.Slider.Value = _color.GetBrightness();
                HSlider.Slider.Value = _color.GetHue();

                ColorDisplayBorder.Background = new SolidColorBrush(_color);

                _isSettingValues = false;
                OnPickColor?.Invoke(value);
            }
        }

        public void LoadCustomPalette(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException($"'{nameof(filename)}' cannot be null or empty.", nameof(filename));
            }

            if (!File.Exists(filename)) return;

            colorPalette = colorPalette.LoadFromXml(filename);

            CustomColorSwatch.SwatchListBox.ItemsSource = colorPalette.CustomColors.ToList();

            // Do regular one too

            colorSwatch1.Clear();
            colorSwatch2.Clear();
            colorSwatch1.AddRange(colorPalette.BuiltInColors.Take(NumColorsFirstSwatch).ToArray());
            colorSwatch2.AddRange(colorPalette.BuiltInColors.Skip(NumColorsFirstSwatch).Take(NumColorsSecondSwatch)
                .ToArray());
            Swatch1.SwatchListBox.ItemsSource = colorSwatch1;
            Swatch2.SwatchListBox.ItemsSource = colorSwatch2;
        }

        public void LoadDefaultCustomPalette() => LoadCustomPalette(Path.Combine(ColorPickerSettings.CustomColorsDirectory,
                ColorPickerSettings.CustomColorsFilename));

        public void SaveCustomPalette(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentException($"'{nameof(filename)}' cannot be null or empty.", nameof(filename));
            }

            colorPalette.CustomColors = CustomColorSwatch.GetColors();

            try
            {
                colorPalette.SaveToXml(filename);
            }
            catch (Exception e)
            {
                // TODO: Log error
                _ = MessageBox.Show($"Could not save the color palette.{Environment.NewLine}{e.Message}{Environment.NewLine}{e.InnerException?.Message ?? string.Empty}");
            }
        }

        internal void CustomColorsChanged()
        {
            if (!ColorPickerSettings.UsingCustomPalette)
            {
                return;
            }

            SaveCustomPalette(ColorPickerSettings.CustomPaletteFilename);
        }

        protected void SampleImageClick(BitmapSource img, Point pos)
        {
            ArgumentNullException.ThrowIfNull(img);

            // https://social.msdn.microsoft.com/Forums/vstudio/en-US/82a5731e-e201-4aaf-8d4b-062b138338fe/getting-pixel-information-from-a-bitmapimage?forum=wpf

            var stride = (int)img.Width * 4;
            var size = (int)img.Height * stride;
            var pixels = new byte[size];

            img.CopyPixels(pixels, stride, 0);

            // Get pixel
            var x = (int)pos.X;
            var y = (int)pos.Y;

            var index = (y * stride) + (4 * x);

            var red = pixels[index];
            var green = pixels[index + 1];
            var blue = pixels[index + 2];
            var alpha = pixels[index + 3];

            Color = Color.FromArgb(alpha, blue, green, red);
        }

        private void ASlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues)
            {
                return;
            }

            _color.A = (byte)value;
            Color = _color;
        }

        private void BSlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues)
            {
                return;
            }

            _color.B = (byte)value;
            Color = _color;
        }

        private void ColorPickerControl_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(SampleImage);
            var img = SampleImage.Source as BitmapSource;

            if (pos.X > 0 && pos.Y > 0 && pos.X < img.PixelWidth && pos.Y < img.PixelHeight)
                SampleImageClick(img, pos);
        }

        private void ColorPickerControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _ = Mouse.Capture(null);
            MouseMove -= ColorPickerControl_MouseMove;
            MouseUp -= ColorPickerControl_MouseUp;
        }

        private void GSlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues)
            {
                return;
            }

            _color.G = (byte)value;
            Color = _color;
        }

        private void HSlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues)
            {
                return;
            }

            var s = _color.GetSaturation();
            var l = _color.GetBrightness();
            var h = (float)value;
            var a = (int)ASlider.Slider.Value;
            _color = Util.FromAhsb(a, h, s, l);

            Color = _color;
        }

        private void LSlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues) return;
            var s = _color.GetSaturation();
            var l = (float)value;
            var h = _color.GetHue();
            var a = (int)ASlider.Slider.Value;
            _color = Util.FromAhsb(a, h, s, l);

            Color = _color;
        }

        private void PickerHueSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateImageForHsv();

        private void RSlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues)
            {
                return;
            }

            _color.R = (byte)value;
            Color = _color;
        }

        private void SampleImage_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _ = Mouse.Capture(this);

            MouseMove += ColorPickerControl_MouseMove;
            MouseUp += ColorPickerControl_MouseUp;
        }

        private void SampleImage2_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(SampleImage2);
            var img = SampleImage2.Source as BitmapSource;
            SampleImageClick(img, pos);
        }

        private void SSlider_OnOnValueChanged(double value)
        {
            if (_isSettingValues)
            {
                return;
            }

            var s = (float)value;
            var l = _color.GetBrightness();
            var h = _color.GetHue();
            var a = (int)ASlider.Slider.Value;
            _color = Util.FromAhsb(a, h, s, l);

            Color = _color;
        }

        private void Swatch_OnOnPickColor(Color color) => Color = color;

        private void UpdateImageForHsv()
        {
            var sliderHue = (float)PickerHueSlider.Value;

            var imageUri = new Uri("pack://application:,,,/NetBannerNG;component/Assets/colorpicker2.png",
                UriKind.Absolute);

            var img = new BitmapImage(imageUri);

            var writableImage = BitmapFactory.ConvertToPbgra32Format(img);

            if (sliderHue is <= 0f or >= 360f)
            {
                // No hue change just return
                SampleImage2.Source = img;
                return;
            }

            using (var context = writableImage.GetBitmapContext())
            {
                long numPixels = img.PixelWidth * img.PixelHeight;

                for (var x = 0; x < img.PixelWidth; x++)
                {
                    for (var y = 0; y < img.PixelHeight; y++)
                    {
                        var pixel = writableImage.GetPixel(x, y);

                        var newHue = sliderHue + pixel.GetHue();
                        if (newHue >= 360)
                        {
                            newHue -= 360;
                        }

                        var color = Util.FromAhsb(255,
                            newHue, pixel.GetSaturation(), pixel.GetBrightness());

                        writableImage.SetPixel(x, y, color);
                    }
                }
            }

            SampleImage2.Source = writableImage;
        }
    }
}
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace NetBannerNG.Controls.ColorPicker
{
    /// <summary>
    ///     Interaction logic for SliderRow.xaml
    /// </summary>
    public partial class SliderRow : UserControl
    {
        public delegate void SliderRowValueChangedHandler(double value);

        private bool updatingValues;

        public SliderRow()
        {
            FormatString = "F2";

            InitializeComponent();
        }

        public string FormatString { get; set; }

        public event SliderRowValueChangedHandler OnValueChanged;

        private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (updatingValues)
            {
                return;
            }

            // Set text box
            var value = Slider.Value;
            updatingValues = true;
            TextBox.Text = value.ToString(FormatString, CultureInfo.InvariantCulture);
            OnValueChanged?.Invoke(value);
            updatingValues = false;
        }

        private void TextBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingValues)
            {
                return;
            }

            var text = TextBox.Text;
            if (!double.TryParse(text, out var parsedValue))
            {
                return;
            }

            updatingValues = true;
            Slider.Value = parsedValue;
            OnValueChanged?.Invoke(parsedValue);
            updatingValues = false;
        }
    }
}
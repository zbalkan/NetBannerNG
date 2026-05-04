using NetBannerNG.Controls.ColorPicker.Code;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NetBannerNG.Controls.ColorPicker
{
    /// <summary>
    ///     Interaction logic for ColorPickRow.xaml
    /// </summary>
    public partial class ColorPickRow : UserControl
    {
        public ColorPickRow()
        {
            InitializeComponent();
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                HexLabel.Text = value.ToHexString();
                ColorDisplayGrid.Background = new SolidColorBrush(value);
            }
        }

        public ColorPickerDialogOptions Options { get; set; }

        public event EventHandler OnPick;

        private Color _color;

        private void PickColorButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ColorPickerWindow.ShowDialog(out var color, Options))
            {
                return;
            }

            Color = color;
            OnPick?.Invoke(this, EventArgs.Empty);
        }
    }
}
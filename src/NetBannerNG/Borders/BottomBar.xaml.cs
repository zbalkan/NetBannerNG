using System.Windows.Input;
using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class BottomBar : BorderBase
    {
        internal BottomBar()
        {
            InitializeComponent();
        }

        internal override void Render(bool needsResize = false)
        {
            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            this.DockBottom();
        }

        protected override void ReadSettings()
        {
            Background = Settings.Instance.CustomBackgroundColor;
            Height = Settings.Instance.BorderSize;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e) => System.Windows.Application.Current.MainWindow.Close();

        private void BorderBase_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Width = 0;
            Height = 0;
            this.Undock();
        }
    }
}
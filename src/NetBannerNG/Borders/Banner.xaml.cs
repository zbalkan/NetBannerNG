using System.Windows.Input;
using NetBannerNG.Utils;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class Banner : BorderBase
    {
        internal Banner()
        {
            InitializeComponent();
            Settings.Instance.Refresh();
            DataContext = Settings.Instance;
        }

        internal override void Render(bool needsResize = false)
        {
            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            Height = Settings.Instance.BannerSize;
            this.DockTop(RootGrid);
        }

        protected override void ReadSettings()
        {
            Settings.Instance.Refresh();
            DataContext = Settings.Instance;

            MinHeight = Settings.Instance.BannerSize;
            MaxHeight = Settings.Instance.BannerSize;
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
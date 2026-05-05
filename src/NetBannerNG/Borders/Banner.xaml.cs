using NetBannerNG.Utils;
using System.Windows.Input;

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
        }

        internal override void Render(bool needsResize = false)
        {
            ReadSettings();
            if (!needsResize)
            {
                return;
            }

            if (IsDocked)
            {
                this.Undock();
            }

            this.DockTop(Grid);
        }

        protected override void ReadSettings()
        {
            LbClassification.Content = Settings.Instance.Classification;
            LbClassification.Foreground = Settings.Instance.CustomForeColor;

            LbHostInformation.Content = Settings.Instance.HostInformation;
            LbHostInformation.Foreground = Settings.Instance.CustomForeColor;

            MinHeight = Settings.Instance.BannerSize;
            MaxHeight = Settings.Instance.BannerSize;
            Background = Settings.Instance.CustomBackgroundColor;
            FontSize = Settings.Instance.FontSize;
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
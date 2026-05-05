using NetBannerNG.Utils;
using System.Windows.Input;

namespace NetBannerNG.Borders
{
    /// <summary>
    ///     Interaction logic for
    /// </summary>
    public partial class RightBar : BorderBase
    {
        internal RightBar()
        {
            InitializeComponent();
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e) => System.Windows.Application.Current.MainWindow.Close();

        internal override void Refresh(bool needsResize = false)
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

            this.DockRight();
        }

        protected override void ReadSettings()
        {
            Background = Settings.Instance.CustomBackgroundColor;
            Width = Settings.Instance.BorderSize;
        }

        private void BorderBase_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Width = 0;
            Height = 0;
            this.Undock();
        }
    }
}
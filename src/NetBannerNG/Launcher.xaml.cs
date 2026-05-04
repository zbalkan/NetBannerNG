using System.Windows;

namespace NetBannerNG
{
    /// <summary>
    /// Interaction logic for Launcher.xaml
    /// </summary>
    public partial class Launcher : Window
    {
        public Launcher()
        {
            InitializeComponent();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Utils.AdminHelper.IsAdmin)
            {
                App.ShutDownGracefully();
            }
            else
            {
                e.Cancel = true;
            }
        }
    }
}
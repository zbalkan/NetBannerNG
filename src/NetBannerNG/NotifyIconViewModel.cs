using NetBannerNG.Utils;
using NetBannerNG.Windows;
using System.Windows;
using System.Windows.Input;

namespace NetBannerNG
{
    /// <summary>
    /// <para>
    ///     Provides bindable properties and commands for the NotifyIcon. In this sample, the
    ///     view model is assigned to the NotifyIcon in XAML. Alternatively, the startup routing
    ///     in App.xaml.cs could have created this view model, and assigned it to the NotifyIcon.
    /// </para>
    /// <para><see href="https://github.com/hardcodet/wpf-notifyicon"/></para>
    ///  </summary>
    public class NotifyIconViewModel
    {
        /// <summary>
        ///     Shows a window, if none is already open.
        /// </summary>
        public ICommand SettingsCommand => new DelegateCommand
        {
            CommandAction = () => new SettingsWindow().Show(),
            CanExecuteFunc = () => AdminHelper.IsAdmin && !GetChildWindows()
                    .Any(
                settingsWindow => settingsWindow.GetType() == typeof(SettingsWindow))
        };

        /// <summary>
        ///     Hides the main window. This command is only enabled if a window is open.
        /// </summary>
        public ICommand ExitCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = App.ShutDownGracefully,
                    CanExecuteFunc = () => AdminHelper.IsAdmin
                };
            }
        }

        /// <summary>
        /// <para>This is required to get rid of the instances of Visual Studio debugging tool for XAML windows,
        /// <see cref="Microsoft.XamlDiagnostics.WpfTap.WpfVisualTreeService.Adorners.AdornerLayerWindow"/>.
        /// Without this, there will be issues with windows handling.
        /// </para>
        /// <para><see href="https://stackoverflow.com/questions/46416123/how-to-properly-ignore-windows-created-by-visual-studio-debugging-tool-for-xaml"/></para>
        /// </summary>
        /// <returns> Returns only the child windows of this application. </returns>
        private static List<Window> GetChildWindows() => Application.Current.Windows.Cast<Window>().Where(w => w.ActualWidth != 0).ToList();
    }
}
using Hardcodet.Wpf.TaskbarNotification;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.Native;
using NetBannerNG.Utils;
using System.Diagnostics;
using System.IO;
using System.Windows;

[assembly: CLSCompliant(true)]

namespace NetBannerNG
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly string _tmpFilePath = Path.Join(UserHelper.UserTempPath, "netbannerng-pipe.tmp");
        private static bool _isClosing;
        private static TaskbarIcon? _notifyIcon;
        internal static NamedPipeClient? Client { get; private set; }

        internal static void ShutDownGracefully()
        {
            if (Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(ShutDownGracefully);
                return;
            }

            try
            {
                _isClosing = true;
                BorderManager.CloseAllBorders();
                PinClearShutdown();
                _notifyIcon?.Dispose(); //the icon would clean up automatically, but this is cleaner
                WindowWatcher.Unwatch();
                MonitorWatcher.Unwatch();
                // ReSharper disable once ConstantConditionalAccessQualifier
                _ = Client?.DisposeAsync().IsCompleted;
                ProcessHelper.Unprotect();
                Current?.Shutdown();
            }
            catch (Exception ex)
            {
                ex.Submit();
                _isClosing = true;
                ProcessHelper.Unprotect();
                Current?.Shutdown();
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                if (!ProcessHelper.EnsureSingleInstance())
                {
                    ShutDownGracefully();
                    return;
                }

                //If no debugger is attached and the argument --debug was passed launch the debugger
                if (e?.Args.Length == 1 && e?.Args[0] == "--debug" && !Debugger.IsAttached)
                {
                    _ = Debugger.Launch();
                }

                // TODO: Make timeout configurable
                Client = new NamedPipeClient();
                var result = await Client.InitializeAsync();
                if (!result)
                {
#if DEBUG
                    _ = MessageBox.Show("Error while connecting to pipe server");
#endif
                    ShutDownGracefully();
                    return;
                }

                base.OnStartup(e);

                BorderManager.Init(IsClearStart());
                BorderManager.InitiateAllBorders();
                PinClearStart();

                // TODO: dispatcher
                WindowWatcher.Watch();
                MonitorWatcher.Watch();
                MonitorWatcher.SetTrigger(BorderManager.Refresh);

                //create the notify icon (it's a resource declared in NotifyIconResources.xaml)
                _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

                ProcessHelper.Protect();
            }
            catch (Exception ex)
            {
                await Dump(ex);
                ShutDownGracefully();
            }
        }

        private static async Task Dump(Exception ex)
        {
            var messageStack = ex.GetMessageStack();
            var path = Path.Combine(UserHelper.UserTempPath, $"netbannerng-dump-{Guid.NewGuid()}");
            await File.WriteAllTextAsync(path, messageStack).ConfigureAwait(false);
            Debug.WriteLine($"Dump file is saved to path: {path}");
            Debug.WriteLine(messageStack);
        }

        private static bool IsClearStart() => !File.Exists(_tmpFilePath);

        private static void PinClearShutdown() => File.Delete(_tmpFilePath);

        private static void PinClearStart() => File.WriteAllText(_tmpFilePath, "1");

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (_isClosing)
            {
                return;
            }
            _isClosing = true;
            ShutDownGracefully();
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            if (_isClosing)
            {
                return;
            }
            e.Cancel = true;
            _isClosing = true;
            ShutDownGracefully();
            e.Cancel = false;
        }
    }
}

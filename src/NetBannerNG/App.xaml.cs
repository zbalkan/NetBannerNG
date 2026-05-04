using NetBannerNG.Common.Extensions;
using NetBannerNG.Services;
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
        private static bool _isClosing;
        private readonly AppLifecycleService _lifecycleService = new();

        internal static async void ShutDownGracefully()
        {
            if (Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                _ = dispatcher.BeginInvoke(ShutDownGracefully);
                return;
            }

            try
            {
                _isClosing = true;
                await ((App)Current!)._lifecycleService.ShutdownRuntimeAsync();
                Current?.Shutdown();
            }
            catch (Exception)
            {
                _isClosing = true;
                Current?.Shutdown();
            }
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                if (!_lifecycleService.EnsureSingleInstance())
                {
                    ShutDownGracefully();
                    return;
                }

                //If no debugger is attached and the argument --debug was passed launch the debugger
                AppLifecycleService.TryLaunchDebugger(e?.Args ?? []);

                // TODO: Make timeout configurable
                var result = await _lifecycleService.InitializePipeClientAsync();
                if (!result)
                {
                    ShutDownGracefully();
                    return;
                }

                base.OnStartup(e);

                await _lifecycleService.InitializeRuntimeAsync();
            }
            catch (Exception ex)
            {
                await Dump(ex);
                ShutDownGracefully();
            }
        }

        private static async Task Dump(Exception ex) => await Task.Run(() => {
            var messageStack = ex.GetMessageStack();
            var path = Path.Combine(UserHelper.UserTempPath, $"netbannerng-dump-{Guid.NewGuid()}");
            File.WriteAllText(path, messageStack);
            Debug.WriteLine($"Dump file is saved to path: {path}");
            Debug.WriteLine(messageStack);
        });

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
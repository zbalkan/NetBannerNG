using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;
using NetBannerNG.Services;
using NetBannerNG.Utils;

[assembly: CLSCompliant(true)]

namespace NetBannerNG
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static bool _isClosing;
        private static int _shutdownStarted;
        private readonly AppLifecycleService _lifecycleService = new();

        internal static void ShutDownGracefully()
        {
            _ = ShutDownGracefullyAsync();
        }

        internal static async Task ShutDownGracefullyAsync()
        {
            if (Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                _ = dispatcher.BeginInvoke(new Func<Task>(ShutDownGracefullyAsync));
                return;
            }

            if (Interlocked.Exchange(ref _shutdownStarted, 1) == 1)
            {
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

                var args = e?.Args ?? Array.Empty<string>();

                // If no debugger is attached and the argument --debug was passed, launch the debugger.
                AppLifecycleService.TryLaunchDebugger(args);

                if (!await TryInitializePipeClientAsync(args))
                {
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

        private async Task<bool> TryInitializePipeClientAsync(string[] args)
        {
            var pipeName = ResolvePipeName(args);
            var initialized = await _lifecycleService.InitializePipeClientAsync(pipeName);
            if (initialized)
            {
                return true;
            }

            ShutDownGracefully();
            return false;
        }

        private static string ResolvePipeName(string[] args)
        {
            var pipeArg = args.FirstOrDefault(a => a.StartsWith("--pipe=", StringComparison.OrdinalIgnoreCase));
            if (pipeArg != null && PipeNaming.TryParsePipeName(pipeArg.Substring("--pipe=".Length), out var fromArg))
            {
                return fromArg;
            }

            return PipeNaming.ForSession((uint)Process.GetCurrentProcess().SessionId);
        }

        private static Task Dump(Exception ex)
        {
            var messageStack = ex.GetMessageStack();
            var path = Path.Combine(UserHelper.UserTempPath, $"netbannerng-dump-{Guid.NewGuid()}");
            File.WriteAllText(path, messageStack);
            Debug.WriteLine($"Dump file is saved to path: {path}");
            Debug.WriteLine(messageStack);
            return Task.CompletedTask;
        }

        private static bool TryBeginShutdown()
        {
            if (_isClosing)
            {
                return false;
            }

            _isClosing = true;
            return true;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (!TryBeginShutdown())
            {
                return;
            }

            ShutDownGracefully();
        }

        private void Application_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            if (!TryBeginShutdown())
            {
                return;
            }

            e.Cancel = true;
            ShutDownGracefully();
        }
    }
}

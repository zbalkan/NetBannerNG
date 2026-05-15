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
    public partial class App : Application, IDisposable
    {
        private static bool _isClosing;
        private static int _shutdownStarted;
        private bool disposedValue;
        private readonly AppLifecycleService _lifecycleService = new();

        internal static void ShutDownGracefully() => _ = ShutDownGracefullyAsync();

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

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                _isClosing = true;
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
                await ((App)Current!)._lifecycleService.ShutdownRuntimeAsync();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
                Current?.Shutdown();
            }
            catch (Exception)
            {
                _isClosing = true;
                Current?.Shutdown();
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                if (!_lifecycleService.EnsureSingleInstance())
                {
                    ShutDownGracefully();
                    return;
                }

                var args = e?.Args ?? Array.Empty<string>();

                if (!AppLifecycleService.EnsureParentIsService())
                {
                    ShutDownGracefully();
                    return;
                }

                // If no debugger is attached and the argument --debug was passed, launch the debugger.
                AppLifecycleService.TryLaunchDebugger(args);

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
                if (!await TryInitializePipeClientAsync(args))
                {
                    return;
                }
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task

                base.OnStartup(e);

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
                await _lifecycleService.InitializeRuntimeAsync();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
            }
            catch (Exception ex)
            {
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
                await Dump(ex);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
                ShutDownGracefully();
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private async Task<bool> TryInitializePipeClientAsync(string[] args)
        {
            var pipeName = ResolvePipeName(args);
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
            var initialized = await _lifecycleService.InitializePipeClientAsync(pipeName);
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
            if (initialized)
            {
                return true;
            }

#pragma warning disable CA1849 // Call async methods when in an async method
            ShutDownGracefully();
#pragma warning restore CA1849 // Call async methods when in an async method
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _lifecycleService?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
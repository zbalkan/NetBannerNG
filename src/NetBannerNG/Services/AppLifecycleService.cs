using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetBannerNG.Common;
using NetBannerNG.Utils;

namespace NetBannerNG.Services
{
    internal interface IMonitorTopologyWatcher
    {
        void Watch(Action refreshAction);

        void Unwatch();
    }

    internal sealed class StaticMonitorTopologyWatcher : IMonitorTopologyWatcher
    {
        public void Watch(Action refreshAction) => MonitorWatcher.Watch(refreshAction);

        public void Unwatch() => MonitorWatcher.Unwatch();
    }

    internal sealed class AppLifecycleService : IDisposable
    {
        private static string TmpFilePath => Path.Combine(UserHelper.UserTempPath, $"netbannerng-pipe-{System.Diagnostics.Process.GetCurrentProcess().SessionId}.tmp");

        private readonly IFullscreenSuppressionService _fullscreenSuppressionService;
        private readonly IMonitorTopologyWatcher _monitorWatcher;
        private readonly IDisplayOverlayOrchestrator _overlayOrchestrator;
        private readonly SemaphoreSlim _runtimeGate = new SemaphoreSlim(1, 1);
        private bool _runtimeStarted;
        private bool disposedValue;

        internal AppLifecycleService()
            : this(new StaticDisplayOverlayOrchestrator(), new FullscreenSuppressionService(), new StaticMonitorTopologyWatcher())
        {
        }

        internal AppLifecycleService(IDisplayOverlayOrchestrator overlayOrchestrator)
            : this(overlayOrchestrator, new FullscreenSuppressionService(), new StaticMonitorTopologyWatcher())
        {
        }

        internal AppLifecycleService(IDisplayOverlayOrchestrator overlayOrchestrator, IFullscreenSuppressionService fullscreenSuppressionService, IMonitorTopologyWatcher monitorWatcher)
        {
            _overlayOrchestrator = overlayOrchestrator;
            _fullscreenSuppressionService = fullscreenSuppressionService;
            _monitorWatcher = monitorWatcher;
        }

        internal NamedPipeClient? Client { get; private set; }

        internal bool EnsureSingleInstance() => ProcessHelper.EnsureSingleInstance();

#pragma warning disable IDE0022 // Use expression body for method
        internal static bool EnsureParentIsService()
        {
#if DEBUG
            return true;
#else
            return ProcessHelper.EnsureParentIsService();
#endif
        }
#pragma warning restore IDE0022 // Use expression body for method

        internal static void TryLaunchDebugger(string[] args)
        {
#if DEBUG
            if (args.Length == 1 && args[0] == "--debug" && !Debugger.IsAttached)
            {
                _ = Debugger.Launch();
            }
#endif
        }

        internal async Task<bool> InitializePipeClientAsync(string pipeName)
        {
            Client = new NamedPipeClient(pipeName);
            return await Client.InitializeAsync().ConfigureAwait(false);
        }

        internal async Task InitializeRuntimeAsync()
        {
            await _runtimeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_runtimeStarted)
                {
                    return;
                }

                var suppressionHooked = false;
                var suppressionStarted = false;
                var monitorWatching = false;
                var processProtected = false;

                try
                {
                    SetSuppressionEventLogSink(message => Client?.SendException(message) ?? Task.CompletedTask);
                    _fullscreenSuppressionService.SuppressionUpdated += _overlayOrchestrator.ApplyFullscreenSuppressionStates;
                    suppressionHooked = true;

                    _overlayOrchestrator.Init(IsClearStart());
                    _overlayOrchestrator.InitiateAllSurfaces();
                    PinClearStart();

                    _fullscreenSuppressionService.Start();
                    suppressionStarted = true;
                    _monitorWatcher.Watch(_overlayOrchestrator.Refresh);
                    monitorWatching = true;
                    ProcessHelper.Protect();
                    processProtected = true;
                    _runtimeStarted = true;
                }
                catch
                {
                    _overlayOrchestrator.BeginShutdown();
                    _overlayOrchestrator.CloseAllSurfaces();

                    if (monitorWatching)
                    {
                        _monitorWatcher.Unwatch();
                    }

                    if (suppressionStarted)
                    {
                        _fullscreenSuppressionService.Stop();
                    }

                    if (suppressionHooked)
                    {
                        _fullscreenSuppressionService.SuppressionUpdated -= _overlayOrchestrator.ApplyFullscreenSuppressionStates;
                    }

                    SetSuppressionEventLogSink(null);

                    if (processProtected)
                    {
                        ProcessHelper.Unprotect();
                    }

                    PinClearShutdown();

                    if (Client is not null)
                    {
                        await Client.DisposeAsync().ConfigureAwait(false);
                        Client = null;
                    }

                    _runtimeStarted = false;
                    throw;
                }
            }
            finally
            {
                _runtimeGate.Release();
            }
        }

        internal async Task ShutdownRuntimeAsync()
        {
            await _runtimeGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_runtimeStarted)
                {
                    _fullscreenSuppressionService.SuppressionUpdated -= _overlayOrchestrator.ApplyFullscreenSuppressionStates;
                    _overlayOrchestrator.BeginShutdown();
                    _monitorWatcher.Unwatch();
                    _fullscreenSuppressionService.Stop();
                    _overlayOrchestrator.CloseAllSurfaces();
                    ProcessHelper.Unprotect();
                }

                SetSuppressionEventLogSink(null);
                PinClearShutdown();
                if (Client is not null)
                {
                    await Client.DisposeAsync().ConfigureAwait(false);
                    Client = null;
                }
                _runtimeStarted = false;
            }
            finally
            {
                _runtimeGate.Release();
            }
        }

        private static bool IsClearStart() => !File.Exists(TmpFilePath);

        private void SetSuppressionEventLogSink(Func<string, Task>? sink)
        {
            if (_fullscreenSuppressionService is FullscreenSuppressionService concrete)
            {
                concrete.EventLogSinkAsync = sink;
            }
        }

        private static void PinClearShutdown()
        {
            if (!File.Exists(TmpFilePath))
            {
                return;
            }

            try
            {
                File.Delete(TmpFilePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; startup path tolerates the marker.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup; startup path tolerates the marker.
            }
        }

        private static void PinClearStart()
        {
            try
            {
                using var _ = new FileStream(TmpFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException)
            {
                // File already exists (not a clear start) or I/O unavailable; both are acceptable.
            }
            catch (UnauthorizedAccessException)
            {
                // File already exists (not a clear start) or I/O unavailable; both are acceptable.
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _runtimeGate?.Dispose();
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
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using NetBannerNG.Common;
using NetBannerNG.Utils;

namespace NetBannerNG.Services
{
    internal interface IMonitorWatcher
    {
        void Watch(Action refreshAction);
        void Unwatch();
    }

    internal sealed class StaticMonitorWatcher : IMonitorWatcher
    {
        public void Watch(Action refreshAction) => MonitorWatcher.Watch(refreshAction);

        public void Unwatch() => MonitorWatcher.Unwatch();
    }

    internal sealed class AppLifecycleService
    {
        private static string TmpFilePath => Path.Combine(UserHelper.UserTempPath, $"netbannerng-pipe-{System.Diagnostics.Process.GetCurrentProcess().SessionId}.tmp");

        private readonly IFullscreenSuppressionService _fullscreenSuppressionService;
        private readonly IMonitorWatcher _monitorWatcher;
        private readonly IDisplayOverlayOrchestrator _overlayOrchestrator;
        private readonly SemaphoreSlim _runtimeGate = new SemaphoreSlim(1, 1);
        private bool _runtimeStarted;


        internal AppLifecycleService()
            : this(new StaticDisplayOverlayOrchestrator(), new FullscreenSuppressionService(), new StaticMonitorWatcher())
        {
        }

        internal AppLifecycleService(IDisplayOverlayOrchestrator overlayOrchestrator)
            : this(overlayOrchestrator, new FullscreenSuppressionService(), new StaticMonitorWatcher())
        {
        }

        internal AppLifecycleService(IDisplayOverlayOrchestrator overlayOrchestrator, IFullscreenSuppressionService fullscreenSuppressionService, IMonitorWatcher monitorWatcher)
        {
            _overlayOrchestrator = overlayOrchestrator;
            _fullscreenSuppressionService = fullscreenSuppressionService;
            _monitorWatcher = monitorWatcher;
        }
        internal NamedPipeClient? Client { get; private set; }

        internal bool EnsureSingleInstance() => ProcessHelper.EnsureSingleInstance();

        internal static bool EnsureParentIsService()
        {
#if DEBUG
            return true;
#else
            return ProcessHelper.EnsureParentIsService();
#endif
        }

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
            return await Client.InitializeAsync();
        }

        internal async Task InitializeRuntimeAsync()
        {
            await _runtimeGate.WaitAsync();
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
                    _overlayOrchestrator.InitiateAllBorders();
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
            await _runtimeGate.WaitAsync();
            try
            {
                if (!_runtimeStarted)
                {
                    return;
                }

                SetSuppressionEventLogSink(null);
                _fullscreenSuppressionService.SuppressionUpdated -= _overlayOrchestrator.ApplyFullscreenSuppressionStates;
                _overlayOrchestrator.BeginShutdown();
                _monitorWatcher.Unwatch();
                _fullscreenSuppressionService.Stop();
                _overlayOrchestrator.CloseAllBorders();
                PinClearShutdown();
                if (Client is not null)
                {
                    await Client.DisposeAsync();
                    Client = null;
                }

                ProcessHelper.Unprotect();
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
                File.WriteAllText(TmpFilePath, "1");
            }
            catch (IOException)
            {
                // Best-effort marker; runtime should continue even if temp storage is unavailable.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort marker; runtime should continue even if temp storage is unavailable.
            }
        }
    }
}

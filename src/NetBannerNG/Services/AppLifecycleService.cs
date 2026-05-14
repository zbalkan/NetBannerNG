using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NetBannerNG.Common;
using NetBannerNG.Utils;

namespace NetBannerNG.Services
{
    internal sealed class AppLifecycleService
    {
        private static string TmpFilePath => Path.Combine(UserHelper.UserTempPath, $"netbannerng-pipe-{System.Diagnostics.Process.GetCurrentProcess().SessionId}.tmp");

        private readonly FullscreenSuppressionService _fullscreenSuppressionService = new();

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

        internal Task InitializeRuntimeAsync()
        {
            _fullscreenSuppressionService.EventLogSinkAsync = message => Client?.SendException(message) ?? Task.CompletedTask;
            _fullscreenSuppressionService.SuppressionUpdated += DisplayOverlayOrchestrator.ApplyFullscreenSuppressionStates;
            DisplayOverlayOrchestrator.Init(IsClearStart());
            DisplayOverlayOrchestrator.InitiateAllBorders();
            PinClearStart();
            _fullscreenSuppressionService.Start();
            MonitorWatcher.Watch(DisplayOverlayOrchestrator.Refresh);
            ProcessHelper.Protect();
            return Task.CompletedTask;
        }

        internal async Task ShutdownRuntimeAsync()
        {
            _fullscreenSuppressionService.EventLogSinkAsync = null;
            _fullscreenSuppressionService.SuppressionUpdated -= DisplayOverlayOrchestrator.ApplyFullscreenSuppressionStates;
            DisplayOverlayOrchestrator.BeginShutdown();
            MonitorWatcher.Unwatch();
            _fullscreenSuppressionService.Stop();
            DisplayOverlayOrchestrator.CloseAllBorders();
            PinClearShutdown();
            if (Client is not null)
            {
                await Client.DisposeAsync();
            }

            ProcessHelper.Unprotect();
        }

        private static bool IsClearStart() => !File.Exists(TmpFilePath);

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

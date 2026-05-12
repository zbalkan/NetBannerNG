using System.ServiceProcess;
using NetBannerNG.Common;

namespace NetBannerNG.Service
{
    /// <inheritdoc />
    /// ref: https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/
    internal class ServiceHost : ServiceBase
    {
        internal enum WatchdogState
        {
            NoSession,
            PipeReady,
            Launching,
            Running,
            Backoff
        }

        private static Thread? _serviceThread;
        private static readonly CancellationTokenSource ServiceStopCts = new();
        private static readonly TimeSpan WatchdogRestartThrottle = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRestartBackoff = TimeSpan.FromSeconds(30);
        private static readonly Random BackoffJitter = new();
        private static DateTime _lastWatchdogRestartAttemptUtc = DateTime.MinValue;
        private static DateTime _nextRestartEligibleUtc = DateTime.MinValue;
        private static int _consecutiveLaunchFailures;
        private static long _connectionChurnCount;
        private static long _failedLaunchCount;
        private static long _deniedClientCount;
        private static long _deniedInboundCount;
        private static WatchdogState _watchdogState = WatchdogState.NoSession;
        private static NamedPipeServer? _pipeServer;
        private static uint _currentSessionId;

        public ServiceHost()
        {
            ServiceName = "NetBannerNGWatchdog";
            EventLogManager.Initialize();
        }

        protected override void OnStart(string[] args) => Run(args);

        protected override void OnStop() => Abort();

        protected override void OnShutdown() => Abort();

        public static void Run(string[] args)
        {
            _serviceThread = new Thread(InitializeServiceThread)
            {
                Name = "NetBannerNG Service Thread",
                IsBackground = true
            };
            _serviceThread.Start();
            Program.Log.LogInformation(EventLogCatalog.ServiceThreadStarted);
        }

        public static void Abort()
        {
            Program.Log.LogInformation(EventLogCatalog.ServiceAbortRequested);
            ProcessHelper.KillAllChildProcess();
            if (!ServiceStopCts.IsCancellationRequested)
            {
                ServiceStopCts.Cancel();
            }
        }

        internal static bool IsStopRequested => ServiceStopCts.IsCancellationRequested;

        private static void InitializeServiceThread()
        {
            try
            {
                InitializeServiceThreadAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, ex.ToString());
                throw;
            }
        }

        private static async Task InitializeServiceThreadAsync()
        {
            _currentSessionId = PrivilegeHelper.GetInteractiveSessionId();
            _pipeServer = new NamedPipeServer(_currentSessionId);
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerCreated);
            TransitionState(WatchdogState.NoSession, WatchdogState.PipeReady, "InitialPipeCreated");

            await using (_pipeServer)
            {
                await _pipeServer.InitializeAsync().ConfigureAwait(false);
                Program.Log.LogInformation(EventLogCatalog.NamedPipeServerInitialized);
                ProcessHelper.KillAllChildProcess();
                while (!ServiceStopCts.IsCancellationRequested)
                {
                    var loopStart = DateTime.UtcNow;
                    await ReconcileSessionPipeServerAsync().ConfigureAwait(false);
                    MonitorChildProcess();
                    var loopDuration = DateTime.UtcNow - loopStart;
                    if (loopDuration > TimeSpan.FromMilliseconds(500))
                    {
                        Program.Log.LogWarning(EventLogCatalog.WatchdogLoopOverrun, loopDuration.TotalMilliseconds);
                    }
                    var jitterMs = (int)(DateTime.UtcNow.Ticks % 35);
                    try
                    {
                        await Task.Delay(250 + jitterMs, ServiceStopCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private static async Task ReconcileSessionPipeServerAsync()
        {
            var latestSessionId = PrivilegeHelper.GetInteractiveSessionId();
            if (!HasSessionChanged(_currentSessionId, latestSessionId))
            {
                return;
            }

            Program.Log.LogInformation(EventLogCatalog.SessionChangedReinitializingPipe, _currentSessionId, latestSessionId);
            _currentSessionId = latestSessionId;
            PrivilegeHelper.ResetSessionOwnerAdminCache();
            TransitionState(_watchdogState, WatchdogState.NoSession, "SessionChanged");

            if (_pipeServer != null)
            {
                await _pipeServer.DisposeAsync().ConfigureAwait(false);
            }

            _pipeServer = new NamedPipeServer(_currentSessionId);
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerCreated);
            await _pipeServer.InitializeAsync().ConfigureAwait(false);
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerInitialized);
            TransitionState(WatchdogState.NoSession, WatchdogState.PipeReady, "SessionPipeReinitialized");
            ProcessHelper.KillAllChildProcess();
        }

        internal static bool HasSessionChanged(uint currentSessionId, uint latestSessionId) => currentSessionId != latestSessionId;

        private static void MonitorChildProcess()
        {
            if (ProcessHelper.IsChildProcessRunning())
            {
                _consecutiveLaunchFailures = 0;
                _nextRestartEligibleUtc = DateTime.MinValue;
                if (_watchdogState != WatchdogState.Running)
                {
                    TransitionState(_watchdogState, WatchdogState.Running, "ChildProcessDetected");
                }
                return;
            }

            var now = DateTime.UtcNow;
            if (!ShouldAttemptRestart(now))
            {
                if (_watchdogState != WatchdogState.Backoff)
                {
                    TransitionState(_watchdogState, WatchdogState.Backoff, "BackoffWindowActive");
                }
                return;
            }
            TransitionState(_watchdogState, WatchdogState.Launching, "WatchdogLaunchAttempt");
            Program.Log.LogWarning(EventLogCatalog.ChildRestartByWatchdog);
            if (!ProcessHelper.InitiateChildProcess())
            {
                RegisterLaunchFailure(now, "LaunchFailed");
                return;
            }

            if (!ProcessHelper.IsChildProcessRunning())
            {
                RegisterLaunchFailure(now, "LaunchNoProcessObserved");
                return;
            }

            _consecutiveLaunchFailures = 0;
            _nextRestartEligibleUtc = DateTime.MinValue;
            TransitionState(WatchdogState.Launching, WatchdogState.Running, "LaunchSucceeded");
        }

        private static bool ShouldAttemptRestart(DateTime now)
        {
            if (now < _nextRestartEligibleUtc)
            {
                return false;
            }

            if ((now - _lastWatchdogRestartAttemptUtc) < WatchdogRestartThrottle)
            {
                return false;
            }

            _lastWatchdogRestartAttemptUtc = now;
            return true;
        }

        private static void RegisterLaunchFailure(DateTime now, string reasonCode)
        {
            Interlocked.Increment(ref _failedLaunchCount);
            _consecutiveLaunchFailures++;
            var delay = CalculateBackoffDelay(_consecutiveLaunchFailures);
            _nextRestartEligibleUtc = now + delay;
            TransitionState(WatchdogState.Launching, WatchdogState.Backoff, reasonCode);
            Program.Log.LogWarning(EventLogCatalog.WatchdogBackoffScheduled, reasonCode, _consecutiveLaunchFailures, delay.TotalSeconds, _failedLaunchCount);
            Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
        }

        internal static TimeSpan CalculateBackoffDelay(int consecutiveLaunchFailures)
        {
            var exponent = Math.Min(Math.Max(consecutiveLaunchFailures, 1) - 1, 5);
            var baseSeconds = Math.Min(1 << exponent, (int)MaxRestartBackoff.TotalSeconds);
            var jitterSeconds = BackoffJitter.NextDouble() * 0.5;
            return TimeSpan.FromSeconds(baseSeconds + jitterSeconds);
        }

        private static void TransitionState(WatchdogState from, WatchdogState to, string reasonCode)
        {
            if (from == to)
            {
                return;
            }

            _watchdogState = to;
            Program.Log.LogInformation(EventLogCatalog.WatchdogStateTransition, from, to, reasonCode);
        }

        internal static void ReportConnectionChurn()
        {
            Interlocked.Increment(ref _connectionChurnCount);
            Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
        }

        internal static void ReportDeniedClient()
        {
            Interlocked.Increment(ref _deniedClientCount);
            Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
        }

        internal static void ReportDeniedInbound()
        {
            Interlocked.Increment(ref _deniedInboundCount);
            Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
        }
    }
}

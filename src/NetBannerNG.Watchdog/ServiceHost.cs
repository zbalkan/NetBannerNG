using System.ServiceProcess;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;

namespace NetBannerNG.Watchdog
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
            Backoff,
            CircuitOpen
        }

        private static Thread? _serviceThread;
        private static readonly CancellationTokenSource ServiceStopCts = new();
        private static readonly ManualResetEventSlim ServiceThreadStopped = new(initialState: true);
        private static readonly TimeSpan ServiceStopTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ChildGracefulExitTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan WatchdogRestartThrottle = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRestartBackoff = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan UiReadinessTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan MinimumStableRuntime = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan MaxPipeStartBackoff = TimeSpan.FromSeconds(30);
        private static DateTime _nextPipeStartEligibleUtc = DateTime.MinValue;
        private static int _consecutivePipeStartFailures;
        private const int MaxConsecutiveLaunchFailures = 5;
        private static readonly Random BackoffJitter = new();
        private static DateTime _lastWatchdogRestartAttemptUtc = DateTime.MinValue;
        private static DateTime _nextRestartEligibleUtc = DateTime.MinValue;
        private static DateTime _circuitOpenUntilUtc = DateTime.MinValue;
        private static DateTime _launchStartedUtc = DateTime.MinValue;
        private static volatile bool _childReady;
        private static int _consecutiveLaunchFailures;
        private static long _connectionChurnCount;
        private static long _failedLaunchCount;
        private static long _deniedClientCount;
        private static long _deniedInboundCount;
        private static WatchdogState _watchdogState = WatchdogState.NoSession;
        private static NamedPipeServer? _pipeServer;
        private static uint _currentSessionId;
        private static bool _recyclePipeRequested;

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
            ServiceThreadStopped.Reset();
            _serviceThread = new Thread(InitializeServiceThread)
            {
                Name = "NetBannerNG Service Thread",
                IsBackground = true
            };

            try
            {
                _serviceThread.Start();
                Program.Log.LogInformation(EventLogCatalog.ServiceThreadStarted);
            }
            catch
            {
                ServiceThreadStopped.Set();
                throw;
            }
        }

        public static void Abort()
        {
            Program.Log.LogInformation(EventLogCatalog.ServiceAbortRequested);
            if (!ServiceStopCts.IsCancellationRequested)
            {
                ServiceStopCts.Cancel();
            }

            var serviceThreadStopped = ServiceThreadStopped.Wait(ServiceStopTimeout);
            if (!serviceThreadStopped)
            {
                Program.Log.LogWarning(EventLogCatalog.ServiceThreadStopTimedOut, ServiceStopTimeout.TotalSeconds);
            }

            // Disposing the pipe server on the service thread tells the UI to run its normal
            // shutdown path, including ABM_REMOVE for every AppBar. Only force termination
            // when that bounded graceful path did not complete.
            if (!serviceThreadStopped || !ProcessHelper.WaitForAllChildProcessesExit(ChildGracefulExitTimeout))
            {
                ProcessHelper.KillAllChildProcess();
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
            finally
            {
                ServiceThreadStopped.Set();
            }
        }

        private static async Task InitializeServiceThreadAsync()
        {
            try
            {
                while (!ServiceStopCts.IsCancellationRequested)
                {
                    var loopStart = DateTime.UtcNow;
                    if (_pipeServer == null)
                    {
                        if (!await TryStartPipeServerAsync("InitialPipeCreated").ConfigureAwait(false))
                        {
                            if (!await DelayLoopAsync(loopStart).ConfigureAwait(false))
                            {
                                break;
                            }
                            continue;
                        }
                    }
                    else if (_pipeServer.IsFaulted)
                    {
                        await RecyclePipeServerAsync("ListenerFaulted").ConfigureAwait(false);
                    }
                    else
                    {
                        await ReconcileSessionPipeServerAsync().ConfigureAwait(false);
                    }

                    if (_pipeServer != null)
                    {
                        MonitorChildProcess();
                    }

                    if (_recyclePipeRequested)
                    {
                        _recyclePipeRequested = false;
                        await RecyclePipeServerAsync("ChildHandshakeFailed").ConfigureAwait(false);
                    }

                    if (!await DelayLoopAsync(loopStart).ConfigureAwait(false))
                    {
                        break;
                    }
                }
            }
            finally
            {
                if (_pipeServer != null)
                {
                    await _pipeServer.DisposeAsync().ConfigureAwait(false);
                    _pipeServer = null;
                }
            }
        }

        private static async Task<bool> DelayLoopAsync(DateTime loopStart)
        {
            var loopDuration = DateTime.UtcNow - loopStart;
            if (loopDuration > TimeSpan.FromMilliseconds(500))
            {
                Program.Log.LogWarning(EventLogCatalog.WatchdogLoopOverrun, loopDuration.TotalMilliseconds);
            }
            var jitterMs = (int)(DateTime.UtcNow.Ticks % 35);
            try
            {
                await Task.Delay(250 + jitterMs, ServiceStopCts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async Task<bool> TryStartPipeServerAsync(string transitionReason)
        {
            var now = DateTime.UtcNow;
            if (now < _nextPipeStartEligibleUtc)
            {
                return false;
            }

            uint sessionId;
            try
            {
                sessionId = PrivilegeHelper.GetInteractiveSessionId();
            }
            catch (InvalidOperationException ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionPending, ex.Message);
                return false;
            }

            if (!NamedPipeServer.TryCreate(sessionId, out var candidate) || candidate == null)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionPending, "Active session user SID is not yet resolvable.");
                return false;
            }

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                await candidate.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // A pipe that cannot be created (name already taken, start timeout) must not
                // crash the service: SCM recovery gives up after two restarts, which would leave
                // the session without a banner. Retry with capped exponential backoff instead.
                await candidate.DisposeAsync().ConfigureAwait(false);
                _consecutivePipeStartFailures++;
                var delay = CalculatePipeStartBackoff(_consecutivePipeStartFailures);
                _nextPipeStartEligibleUtc = now + delay;
                Program.Log.LogError(EventLogCatalog.PipeServerStartFailed, sessionId, delay.TotalSeconds, ex.GetMessageStack());
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types

            _consecutivePipeStartFailures = 0;
            _nextPipeStartEligibleUtc = DateTime.MinValue;
            _currentSessionId = sessionId;
            _pipeServer = candidate;
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerCreated);
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerInitialized);
            TransitionState(WatchdogState.NoSession, WatchdogState.PipeReady, transitionReason);
            ProcessHelper.KillAllChildProcess();
            _childReady = false;
            _launchStartedUtc = DateTime.MinValue;
            return true;
        }

        private static async Task ReconcileSessionPipeServerAsync()
        {
            uint latestSessionId;
            try
            {
                latestSessionId = PrivilegeHelper.GetInteractiveSessionId();
            }
            catch (InvalidOperationException ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionPending, ex.Message);
                return;
            }

            if (!HasSessionChanged(_currentSessionId, latestSessionId))
            {
                return;
            }

            Program.Log.LogInformation(EventLogCatalog.SessionChangedReinitializingPipe, _currentSessionId, latestSessionId);
            PrivilegeHelper.ResetSessionOwnerAdminCache();
            _consecutivePipeStartFailures = 0;
            _nextPipeStartEligibleUtc = DateTime.MinValue;
            TransitionState(_watchdogState, WatchdogState.NoSession, "SessionChanged");

            if (_pipeServer != null)
            {
                await _pipeServer.DisposeAsync().ConfigureAwait(false);
                _pipeServer = null;
            }

            _currentSessionId = latestSessionId;
            _ = await TryStartPipeServerAsync("SessionPipeReinitialized").ConfigureAwait(false);
        }

        private static async Task RecyclePipeServerAsync(string reasonCode)
        {
            Program.Log.LogWarning(EventLogCatalog.PipeServerRecycled, _currentSessionId, reasonCode);
            TransitionState(_watchdogState, WatchdogState.NoSession, reasonCode);
            if (_pipeServer != null)
            {
                await _pipeServer.DisposeAsync().ConfigureAwait(false);
                _pipeServer = null;
            }
        }

        internal static TimeSpan CalculatePipeStartBackoff(int consecutiveFailures)
        {
            var exponent = Math.Min(Math.Max(consecutiveFailures, 1) - 1, 5);
            return TimeSpan.FromSeconds(Math.Min(1 << exponent, MaxPipeStartBackoff.TotalSeconds));
        }

        internal static bool IsHandshakeFailure(string reasonCode) =>
            string.Equals(reasonCode, "ReadinessTimeout", StringComparison.Ordinal)
            || string.Equals(reasonCode, "ExitedBeforeReady", StringComparison.Ordinal);

        internal static bool HasSessionChanged(uint currentSessionId, uint latestSessionId) => currentSessionId != latestSessionId;

        private static void MonitorChildProcess()
        {
            var now = DateTime.UtcNow;
            if (ProcessHelper.IsChildProcessRunning())
            {
                if (_childReady)
                {
                    if (now - _launchStartedUtc < MinimumStableRuntime)
                    {
                        if (_watchdogState != WatchdogState.Running)
                        {
                            TransitionState(_watchdogState, WatchdogState.Running, "ChildReadyStabilizing");
                        }
                        return;
                    }

                    _consecutiveLaunchFailures = 0;
                    _nextRestartEligibleUtc = DateTime.MinValue;
                    _launchStartedUtc = DateTime.MinValue;
                    if (_watchdogState != WatchdogState.Running)
                    {
                        TransitionState(_watchdogState, WatchdogState.Running, "ChildReadyStable");
                    }
                    return;
                }

                if (_launchStartedUtc != DateTime.MinValue && now - _launchStartedUtc >= UiReadinessTimeout)
                {
                    ProcessHelper.KillAllChildProcess();
                    RegisterLaunchFailure(now, "ReadinessTimeout");
                }
                return;
            }

            if (_launchStartedUtc != DateTime.MinValue)
            {
                RegisterLaunchFailure(now, _childReady ? "ExitedDuringStabilityWindow" : "ExitedBeforeReady");
                return;
            }

            _childReady = false;
            if (IsCircuitOpen(now))
            {
                if (_watchdogState != WatchdogState.CircuitOpen)
                {
                    TransitionState(_watchdogState, WatchdogState.CircuitOpen, "FailureCircuitOpen");
                }
                return;
            }

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
            _launchStartedUtc = now;
            _childReady = false;
            if (!ProcessHelper.InitiateChildProcess())
            {
                RegisterLaunchFailure(now, "LaunchFailed");
                return;
            }

            if (!ProcessHelper.IsChildProcessRunning())
            {
                RegisterLaunchFailure(now, "LaunchNoProcessObserved");
            }
        }

        private static bool IsCircuitOpen(DateTime now)
        {
            if (now < _circuitOpenUntilUtc)
            {
                return true;
            }

            if (_circuitOpenUntilUtc != DateTime.MinValue)
            {
                _circuitOpenUntilUtc = DateTime.MinValue;
                _consecutiveLaunchFailures = 0;
                Program.Log.LogInformation(EventLogCatalog.WatchdogCircuitClosed);
            }

            return false;
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
            _childReady = false;
            _launchStartedUtc = DateTime.MinValue;

            // A child that never completes the pipe handshake may be talking to a listener
            // that died silently after its last disconnect (H.Pipes ends its loop on some
            // pipe-creation errors without notice). Recreate the pipe before the next launch.
            if (IsHandshakeFailure(reasonCode))
            {
                _recyclePipeRequested = true;
            }

            if (ShouldOpenRecoveryCircuit(_consecutiveLaunchFailures))
            {
                _circuitOpenUntilUtc = now + CircuitOpenDuration;
                TransitionState(_watchdogState, WatchdogState.CircuitOpen, reasonCode);
                Program.Log.LogError(EventLogCatalog.WatchdogCircuitOpened, reasonCode, _consecutiveLaunchFailures, CircuitOpenDuration.TotalSeconds);
                Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
                return;
            }

            var delay = CalculateBackoffDelay(_consecutiveLaunchFailures);
            _nextRestartEligibleUtc = now + delay;
            TransitionState(WatchdogState.Launching, WatchdogState.Backoff, reasonCode);
            Program.Log.LogWarning(EventLogCatalog.WatchdogBackoffScheduled, reasonCode, _consecutiveLaunchFailures, delay.TotalSeconds, _failedLaunchCount);
            Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
        }

        internal static bool IsLaunchAwaitingReadiness(DateTime launchStartedUtc, bool childReady) =>
            launchStartedUtc != DateTime.MinValue && !childReady;

        internal static bool ShouldOpenRecoveryCircuit(int consecutiveLaunchFailures) =>
            consecutiveLaunchFailures >= MaxConsecutiveLaunchFailures;

        internal static TimeSpan CalculateBackoffDelay(int consecutiveLaunchFailures)
        {
            var exponent = Math.Min(Math.Max(consecutiveLaunchFailures, 1) - 1, 5);
            var baseSeconds = Math.Min(1 << exponent, (int)MaxRestartBackoff.TotalSeconds);
#pragma warning disable CA5394 // Do not use insecure randomness
            var jitterSeconds = BackoffJitter.NextDouble() * 0.5;
#pragma warning restore CA5394 // Do not use insecure randomness
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

        internal static void ReportChildReady(uint sessionId)
        {
            if (sessionId != _currentSessionId || _watchdogState != WatchdogState.Launching)
            {
                return;
            }

            _childReady = true;
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
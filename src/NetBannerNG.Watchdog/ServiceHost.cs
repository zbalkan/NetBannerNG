using System.ServiceProcess;
using NetBannerNG.Common;

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
            Backoff
        }

        private sealed class SessionScope : IAsyncDisposable
        {
            public uint SessionId { get; }
            public NamedPipeServer PipeServer { get; }
            public WatchdogState State { get; set; } = WatchdogState.PipeReady;
            public int ConsecutiveLaunchFailures { get; set; }
            public DateTime NextRestartEligibleUtc { get; set; } = DateTime.MinValue;
            public DateTime LastRestartAttemptUtc { get; set; } = DateTime.MinValue;

            public SessionScope(uint sessionId, NamedPipeServer pipeServer)
            {
                SessionId = sessionId;
                PipeServer = pipeServer;
            }

            public async ValueTask DisposeAsync()
            {
                await PipeServer.DisposeAsync().ConfigureAwait(false);
                GC.SuppressFinalize(this);
            }
        }

        private static Thread? _serviceThread;
        private static readonly CancellationTokenSource ServiceStopCts = new();
        private static readonly TimeSpan WatchdogRestartThrottle = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxRestartBackoff = TimeSpan.FromSeconds(30);
        private static readonly Random BackoffJitter = new();

        private static long _connectionChurnCount;
        private static long _failedLaunchCount;
        private static long _deniedClientCount;
        private static long _deniedInboundCount;

        private static readonly Dictionary<uint, SessionScope> ActiveSessions = new();
        private static readonly object SessionsLock = new();

        public ServiceHost()
        {
            ServiceName = "NetBannerNGWatchdog";
            CanHandleSessionChangeEvent = true;
            EventLogManager.Initialize();
        }

        protected override void OnStart(string[] args) => Run(args);

        protected override void OnStop() => Abort();

        protected override void OnShutdown() => Abort();

        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            var sessionId = (uint)changeDescription.SessionId;
            switch (changeDescription.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    Program.Log.LogInformation(EventLogCatalog.SessionLogonObserved, sessionId, changeDescription.Reason);
                    _ = Task.Run(() => HandleSessionStartAsync(sessionId).GetAwaiter().GetResult());
                    break;

                case SessionChangeReason.RemoteConnect:
                case SessionChangeReason.ConsoleConnect:
                    Program.Log.LogInformation(EventLogCatalog.SessionLogonObserved, sessionId, changeDescription.Reason);
                    _ = Task.Run(() => HandleSessionConnectAsync(sessionId).GetAwaiter().GetResult());
                    break;

                case SessionChangeReason.SessionLogoff:
                case SessionChangeReason.ConsoleDisconnect:
                    Program.Log.LogInformation(EventLogCatalog.SessionLogoffObserved, sessionId, changeDescription.Reason);
                    _ = Task.Run(() => HandleSessionEndAsync(sessionId).GetAwaiter().GetResult());
                    break;

                // RemoteDisconnect: leave GUI running per F2 — no action.
                // SessionLock / SessionUnlock: no action.
            }
        }

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
            try
            {
                // F1: enumerate all currently active sessions and spawn one GUI each.
                await InitializeAllSessionsAsync().ConfigureAwait(false);

                while (!ServiceStopCts.IsCancellationRequested)
                {
                    var loopStart = DateTime.UtcNow;
                    MonitorAllChildProcesses();

                    var loopDuration = DateTime.UtcNow - loopStart;
                    if (loopDuration > TimeSpan.FromMilliseconds(500))
                    {
                        Program.Log.LogWarning(EventLogCatalog.WatchdogLoopOverrun, loopDuration.TotalMilliseconds);
                    }

                    if (!await DelayLoopAsync().ConfigureAwait(false))
                    {
                        break;
                    }
                }
            }
            finally
            {
                List<SessionScope> toDispose;
                lock (SessionsLock)
                {
                    toDispose = ActiveSessions.Values.ToList();
                    ActiveSessions.Clear();
                }

                foreach (var scope in toDispose)
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task<bool> DelayLoopAsync()
        {
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

        private static async Task InitializeAllSessionsAsync()
        {
            IEnumerable<uint> sessionIds;
            try
            {
                sessionIds = PrivilegeHelper.EnumerateInteractiveSessions().ToList();
            }
            catch (Exception ex)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionPending, ex.Message);
                return;
            }

            foreach (var sessionId in sessionIds)
            {
                await HandleSessionStartAsync(sessionId).ConfigureAwait(false);
            }
        }

        private static async Task HandleSessionStartAsync(uint sessionId)
        {
            // F5: never launch into session 0 or the sentinel value.
            if (sessionId == 0 || sessionId == 0xFFFF)
            {
                return;
            }

            // N2: idempotency — if already tracking this session, skip.
            lock (SessionsLock)
            {
                if (ActiveSessions.ContainsKey(sessionId))
                {
                    return;
                }
            }

            if (!await TryStartSessionScopeAsync(sessionId).ConfigureAwait(false))
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionPending, $"Failed to initialize session scope for session {sessionId}.");
            }
        }

        private static async Task HandleSessionConnectAsync(uint sessionId)
        {
            // F2 WTS_REMOTE_CONNECT / WTS_CONSOLE_CONNECT: spawn only if no GUI is already running.
            bool hasExisting;
            lock (SessionsLock)
            {
                hasExisting = ActiveSessions.ContainsKey(sessionId);
            }

            if (!hasExisting)
            {
                await HandleSessionStartAsync(sessionId).ConfigureAwait(false);
            }
        }

        private static async Task HandleSessionEndAsync(uint sessionId)
        {
            // F2 WTS_SESSION_LOGOFF / WTS_CONSOLE_DISCONNECT: kill GUI and dispose pipe server.
            SessionScope? scope;
            lock (SessionsLock)
            {
                ActiveSessions.TryGetValue(sessionId, out scope);
                ActiveSessions.Remove(sessionId);
            }

            if (scope == null)
            {
                return;
            }

            ProcessHelper.KillChildProcessInSession(sessionId);
            await scope.DisposeAsync().ConfigureAwait(false);
            Program.Log.LogInformation(EventLogCatalog.GuiTerminatedInSession, sessionId);
        }

        private static async Task<bool> TryStartSessionScopeAsync(uint sessionId)
        {
            if (!NamedPipeServer.TryCreate(sessionId, out var pipeServer) || pipeServer == null)
            {
                Program.Log.LogWarning(EventLogCatalog.PipeSessionPending, $"Session {sessionId} user SID is not yet resolvable.");
                return false;
            }

            try
            {
                await pipeServer.InitializeAsync().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                Program.Log.LogError(EventLogCatalog.PipeExceptionOccurred, ex.ToString());
                await pipeServer.DisposeAsync().ConfigureAwait(false);
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types

            var scope = new SessionScope(sessionId, pipeServer);

            // Double-checked locking: another task may have raced to create the same session scope.
            lock (SessionsLock)
            {
                if (ActiveSessions.ContainsKey(sessionId))
                {
                    _ = pipeServer.DisposeAsync().AsTask();
                    return true;
                }

                ActiveSessions[sessionId] = scope;
            }

            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerCreated);
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerInitialized);

            // N3: TryReattachSessionProcess inside InitiateChildProcess handles the case where
            // a GUI is already running (service restart).  If not found, a fresh one is spawned.
            _ = ProcessHelper.InitiateChildProcess(sessionId);
            return true;
        }

        private static void MonitorAllChildProcesses()
        {
            List<SessionScope> scopes;
            lock (SessionsLock)
            {
                scopes = ActiveSessions.Values.ToList();
            }

            foreach (var scope in scopes)
            {
                MonitorChildProcess(scope);
            }
        }

        private static void MonitorChildProcess(SessionScope scope)
        {
            if (ProcessHelper.IsChildProcessRunning(scope.SessionId))
            {
                scope.ConsecutiveLaunchFailures = 0;
                scope.NextRestartEligibleUtc = DateTime.MinValue;
                if (scope.State != WatchdogState.Running)
                {
                    TransitionState(scope, scope.State, WatchdogState.Running, "ChildProcessDetected");
                }

                return;
            }

            var now = DateTime.UtcNow;
            if (!ShouldAttemptRestart(scope, now))
            {
                if (scope.State != WatchdogState.Backoff)
                {
                    TransitionState(scope, scope.State, WatchdogState.Backoff, "BackoffWindowActive");
                }

                return;
            }

            TransitionState(scope, scope.State, WatchdogState.Launching, "WatchdogLaunchAttempt");
            Program.Log.LogWarning(EventLogCatalog.ChildRestartByWatchdog);

            if (!ProcessHelper.InitiateChildProcess(scope.SessionId))
            {
                RegisterLaunchFailure(scope, now, "LaunchFailed");
                return;
            }

            if (!ProcessHelper.IsChildProcessRunning(scope.SessionId))
            {
                RegisterLaunchFailure(scope, now, "LaunchNoProcessObserved");
                return;
            }

            scope.ConsecutiveLaunchFailures = 0;
            scope.NextRestartEligibleUtc = DateTime.MinValue;
            TransitionState(scope, WatchdogState.Launching, WatchdogState.Running, "LaunchSucceeded");
        }

        private static bool ShouldAttemptRestart(SessionScope scope, DateTime now)
        {
            if (now < scope.NextRestartEligibleUtc)
            {
                return false;
            }

            if ((now - scope.LastRestartAttemptUtc) < WatchdogRestartThrottle)
            {
                return false;
            }

            scope.LastRestartAttemptUtc = now;
            return true;
        }

        private static void RegisterLaunchFailure(SessionScope scope, DateTime now, string reasonCode)
        {
            Interlocked.Increment(ref _failedLaunchCount);
            scope.ConsecutiveLaunchFailures++;
            var delay = CalculateBackoffDelay(scope.ConsecutiveLaunchFailures);
            scope.NextRestartEligibleUtc = now + delay;
            TransitionState(scope, WatchdogState.Launching, WatchdogState.Backoff, reasonCode);
            Program.Log.LogWarning(EventLogCatalog.WatchdogBackoffScheduled, reasonCode, scope.ConsecutiveLaunchFailures, delay.TotalSeconds, _failedLaunchCount);
            Program.Log.LogInformation(EventLogCatalog.WatchdogHealthCounters, _connectionChurnCount, _failedLaunchCount, _deniedClientCount, _deniedInboundCount);
        }

        internal static TimeSpan CalculateBackoffDelay(int consecutiveLaunchFailures)
        {
            var exponent = Math.Min(Math.Max(consecutiveLaunchFailures, 1) - 1, 5);
            var baseSeconds = Math.Min(1 << exponent, (int)MaxRestartBackoff.TotalSeconds);
#pragma warning disable CA5394 // Do not use insecure randomness
            var jitterSeconds = BackoffJitter.NextDouble() * 0.5;
#pragma warning restore CA5394 // Do not use insecure randomness
            return TimeSpan.FromSeconds(baseSeconds + jitterSeconds);
        }

        private static void TransitionState(SessionScope scope, WatchdogState from, WatchdogState to, string reasonCode)
        {
            if (from == to)
            {
                return;
            }

            scope.State = to;
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

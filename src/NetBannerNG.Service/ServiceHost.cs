using System.ServiceProcess;
using NetBannerNG.Common;

namespace NetBannerNG.Service
{
    /// <inheritdoc />
    /// ref: https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/
    internal class ServiceHost : ServiceBase
    {
        private static Thread? _serviceThread;
        private static bool _stopping;
        private static readonly TimeSpan WatchdogRestartThrottle = TimeSpan.FromSeconds(5);
        private static DateTime _lastWatchdogRestartAttemptUtc = DateTime.MinValue;
        internal static NamedPipeServer? pipeServer;

        public ServiceHost()
        {
            ServiceName = "NetBannerNG Service";
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
            _stopping = true;
        }

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
            var sessionId = PrivilegeHelper.GetInteractiveSessionId();
            pipeServer = new NamedPipeServer(sessionId);
            Program.Log.LogInformation(EventLogCatalog.NamedPipeServerCreated);

            await using (pipeServer)
            {
                await pipeServer.InitializeAsync().ConfigureAwait(false);
                Program.Log.LogInformation(EventLogCatalog.NamedPipeServerInitialized);
                ProcessHelper.KillAllChildProcess();
                while (!_stopping)
                {
                    MonitorChildProcess();
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }

        private static void MonitorChildProcess()
        {
            if (ProcessHelper.IsChildProcessRunning())
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (!ShouldAttemptRestart(now))
            {
                return;
            }
            Program.Log.LogWarning(EventLogCatalog.ChildRestartByWatchdog);
            ProcessHelper.InitiateChildProcess();
        }

        private static bool ShouldAttemptRestart(DateTime now)
        {
            if ((now - _lastWatchdogRestartAttemptUtc) < WatchdogRestartThrottle)
            {
                return false;
            }

            _lastWatchdogRestartAttemptUtc = now;
            return true;
        }
    }
}
using System.ServiceProcess;

namespace NetBannerNG.Service
{
    /// <inheritdoc />
    /// ref: https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/
    internal class ServiceHost : ServiceBase
    {
        private static Thread? _serviceThread;
        private static bool _stopping;
        private static DateTime _lastWatchdogRestartAttemptUtc = DateTime.MinValue;
        internal static NamedPipeServer? pipeServer;

        public ServiceHost()
        {
            ServiceName = "NetBannerNG Service";
            EventLogManager.Initialize();
        }

        protected override void OnStart(string[] args)
        {
            Run(args);
        }

        protected override void OnStop()
        {
            Abort();
        }

        protected override void OnShutdown()
        {
            Abort();
        }

        public static void Run(string[] args)
        {
            _serviceThread = new Thread(InitializeServiceThread)
            {
                Name = "NetBannerNG Service Thread",
                IsBackground = true
            };
            _serviceThread.Start();
            Program.Log.LogInformation("[NBNG-2000] Service host thread started.");
        }

        public static void Abort()
        {
            Program.Log.LogInformation("[NBNG-2001] Service abort requested.");
            ProcessHelper.KillAllChildProcess();
            _stopping = true;
        }

        private static async void InitializeServiceThread()
        {
            //Console.WriteLine("Simulating checks with 3-second-sleep.");
            // TODO: Make timeout configurable
            //Thread.Sleep(3000);
            pipeServer = new NamedPipeServer();
            Program.Log.LogInformation("[NBNG-2002] Named pipe server created.");

            await using (pipeServer)
            {
                await pipeServer.InitializeAsync().ConfigureAwait(false);
                Program.Log.LogInformation("[NBNG-2003] Named pipe server initialized.");

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
            if ((now - _lastWatchdogRestartAttemptUtc) < TimeSpan.FromSeconds(5))
            {
                return;
            }

            _lastWatchdogRestartAttemptUtc = now;
            Program.Log.LogWarning("[NBNG-2004] Child process not found. Watchdog is restarting NetBannerNG.");
            ProcessHelper.InitiateChildProcess();
        }
    }
}

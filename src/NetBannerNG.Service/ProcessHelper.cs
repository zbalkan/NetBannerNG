using System.Diagnostics;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Service
{
    public static class ProcessHelper
    {
        private const string ChildProcessName = "NetBannerNG";

        public static void InitiateChildProcess()
        {
            var psi = new ProcessStartInfo();
            var sessionId = PrivilegeHelper.GetInteractiveSessionId();
            var pipeName = PipeNaming.ForSession(sessionId);
            var path = GetChildProcessPath();
            if (!File.Exists(path))
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"File not found: {path}");
                return;
            }

            path = Path.GetFullPath(path);
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"Invalid executable path: {path}");
                return;
            }

            psi.FileName = path;
            psi.Arguments = $"--pipe={pipeName}";
            Program.Log.LogInformation(EventLogCatalog.ProcessStarting, psi.FileName);
            if (Environment.UserInteractive)
            {
                try
                {
                    _ = Process.Start(psi);
                    Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccesfully, psi.FileName);
                }
                catch (Exception ex)
                {
                    Program.Log.LogError(EventLogCatalog.ProcessStartFailed, psi.FileName, ex);
                }
                return;
            }

            if (!psi.RunAsActiveUser())
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"Failed to start {psi.FileName}", new Exception("Failed to run as active user."));
            }
        }

        private static string GetChildProcessPath()
        {
#if DEBUG
            return Path.Combine(new DirectoryInfo(path: AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, @"NetBannerNG\bin\Debug\net481\NetBannerNG.exe");            //psi.Arguments = "--debug";
#else
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetBannerNG.exe");
#endif
        }

        public static void KillAllChildProcess() => Process.GetProcesses().Where(p => p.ProcessName == ChildProcessName).ToList().ForEach(p => p.Kill());

        public static bool IsChildProcessRunning() => Process.GetProcessesByName(ChildProcessName).Any();
    }
}
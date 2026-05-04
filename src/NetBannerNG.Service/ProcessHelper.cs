using NetBannerNG.Common.Extensions;
using System.Diagnostics;

namespace NetBannerNG.Service
{
    public static class ProcessHelper
    {
        private const string ChildProcessName = "NetBannerNG";

        public static void InitiateChildProcess()
        {
            var psi = new ProcessStartInfo();
            string path;
#if DEBUG
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            path = Path.Combine(new DirectoryInfo(path: AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, @"NetBannerNG\bin\Debug\net481\NetBannerNG.exe");            //psi.Arguments = "--debug";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#else
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetBannerNG.exe");
#endif
            if (!File.Exists(path))
            {
                Console.WriteLine($"File not found: {path}");
                return;
            }

            path = Path.GetFullPath(path);
            if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Invalid executable path: {path}");
                return;
            }

            psi.FileName = path;
            Console.WriteLine($"Starting process:{psi.FileName}");
            Program.Log.LogInformation($"Starting process:{psi.FileName}");
            if (Environment.UserInteractive)
            {
                _ = Process.Start(psi);
                return;
            }

            if (!psi.RunAsActiveUser())
            {
                Console.WriteLine($"Failed to start {psi.FileName}");
            }
        }

        public static void KillAllChildProcess()
        {
            Process.GetProcesses().Where(p => p.ProcessName == ChildProcessName).ToList().ForEach(p => p.Kill());
        }

        public static bool IsChildProcessRunning()
        {
            return Process.GetProcessesByName(ChildProcessName).Any();
        }
    }
}

using System.Diagnostics;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Service
{
    public static class ProcessHelper
    {
        private const string ChildProcessName = "NetBannerNG";
        private static readonly HashSet<int> LaunchedProcessIds = new();
        private static readonly object LaunchSync = new();

        public static bool InitiateChildProcess()
        {
            var sessionId = PrivilegeHelper.GetInteractiveSessionId();
            var pipeName = PipeNaming.ForSession(sessionId);
            if (!TryResolveValidatedChildProcessPath(out var path))
            {
                return false;
            }

            var psi = BuildChildProcessStartInfo(path, pipeName);
            Program.Log.LogInformation(EventLogCatalog.ProcessStarting, psi.FileName);
            if (Environment.UserInteractive)
            {
                try
                {
                    var process = Process.Start(psi);
                    if (process != null)
                    {
                        TrackLaunchedProcess(process);
                    }
                    Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccesfully, psi.FileName);
                    return true;
                }
                catch (Exception ex)
                {
                    Program.Log.LogError(EventLogCatalog.ProcessStartFailed, psi.FileName, ex);
                    return false;
                }
            }

            if (!psi.RunAsActiveUser())
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"Failed to start {psi.FileName}", new Exception("Failed to run as active user."));
                return false;
            }

            TrackLaunchedProcessesBySession((int)sessionId);
            Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccesfully, psi.FileName);
            return true;
        }

        public static bool TryInitiateChildProcess()
        {
            return InitiateChildProcess();
        }

        public static void KillAllChildProcess()
        {
            foreach (var process in GetChildProcesses())
            {
                try
                {
                    process.Kill();
                    UntrackLaunchedProcess(process.Id);
                }
                catch (Exception ex)
                {
                    Program.Log.LogWarning(EventLogCatalog.ProcessFailedToKill, process.Id, ex.GetMessageStack());
                }
            }
        }

        public static bool IsChildProcessRunning() => GetChildProcesses().Any();

        private static ProcessStartInfo BuildChildProcessStartInfo(string path, string pipeName) =>
            new()
            {
                FileName = path,
                Arguments = $"--pipe={pipeName}"
            };

        private static bool TryResolveValidatedChildProcessPath(out string validatedPath)
        {
            validatedPath = GetChildProcessPath();
            if (!File.Exists(validatedPath))
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"File not found: {validatedPath}");
                return false;
            }

            validatedPath = Path.GetFullPath(validatedPath);
            if (!validatedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"Invalid executable path: {validatedPath}");
                return false;
            }

            return true;
        }

        private static string GetChildProcessPath()
        {
#if DEBUG
            return Path.Combine(new DirectoryInfo(path: AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, @"NetBannerNG\bin\Debug\net481\NetBannerNG.exe");
#else
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetBannerNG.exe");
#endif
        }

        private static IEnumerable<Process> GetChildProcesses()
        {
            var expectedPath = Path.GetFullPath(GetChildProcessPath());
            var interactiveSessionId = (int)PrivilegeHelper.GetInteractiveSessionId();
            return Process.GetProcessesByName(ChildProcessName)
                .Where(process => IsExpectedChildProcess(process, expectedPath, interactiveSessionId));
        }

        private static bool IsExpectedChildProcess(Process process, string expectedPath, int interactiveSessionId)
        {
            try
            {
                if (process.SessionId != interactiveSessionId)
                {
                    return false;
                }

                var candidatePath = Path.GetFullPath(process.MainModule?.FileName ?? string.Empty);
                if (!candidatePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                lock (LaunchSync)
                {
                    return LaunchedProcessIds.Contains(process.Id);
                }
            }
            catch (Exception ex)
            {
                Program.Log.LogInformation(EventLogCatalog.ProcessFailedToKill, process.Id, $"Identity check failed: {ex.GetType().Name}");
                return false;
            }
        }

        private static void TrackLaunchedProcess(Process process)
        {
            lock (LaunchSync)
            {
                LaunchedProcessIds.Add(process.Id);
            }
        }

        private static void TrackLaunchedProcessesBySession(int interactiveSessionId)
        {
            foreach (var process in Process.GetProcessesByName(ChildProcessName).Where(p => p.SessionId == interactiveSessionId))
            {
                TrackLaunchedProcess(process);
            }
        }

        private static void UntrackLaunchedProcess(int processId)
        {
            lock (LaunchSync)
            {
                LaunchedProcessIds.Remove(processId);
            }
        }
    }
}

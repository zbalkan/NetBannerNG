using System.Diagnostics;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Service
{
    public static class ProcessHelper
    {
        private const string ChildProcessName = "NetBannerNG";

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
                    _ = Process.Start(psi);
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
                return candidatePath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}

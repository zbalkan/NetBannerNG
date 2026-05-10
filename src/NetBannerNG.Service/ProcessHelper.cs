using System.Diagnostics;
using System.Management;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Service
{
    public static class ProcessHelper
    {
        private const string ChildProcessName = "NetBannerNG";

        private sealed class LaunchedProcessInfo
        {
            public DateTime StartTimeUtc { get; set; }
            public string PipeName { get; set; } = string.Empty;
        }

        private static readonly Dictionary<int, LaunchedProcessInfo> LaunchedProcesses = new();
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
                        TrackLaunchedProcess(process, pipeName);
                    }
                    Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccessfully, psi.FileName);
                    return true;
                }
                catch (Exception ex)
                {
                    Program.Log.LogError(EventLogCatalog.ProcessStartFailed, psi.FileName, ex);
                    return false;
                }
            }

            var existingCandidates = CaptureCandidateProcessIds((int)sessionId);
            if (!psi.RunAsActiveUser())
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, $"Failed to start {psi.FileName}", new Exception("Failed to run as active user."));
                return false;
            }

            TrackNewlyLaunchedProcesses((int)sessionId, existingCandidates, pipeName);
            Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccessfully, psi.FileName);
            return true;
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
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, validatedPath, "File not found.");
                return false;
            }

            validatedPath = Path.GetFullPath(validatedPath);
            if (!validatedPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Program.Log.LogError(EventLogCatalog.ProcessStartFailed, validatedPath, "Path must target an .exe file.");
                return false;
            }

            return true;
        }

#pragma warning disable IDE0022 // Use expression body for method
        private static string GetChildProcessPath()
        {
#if DEBUG
            return Path.Combine(new DirectoryInfo(path: AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.Parent.FullName, @"NetBannerNG\bin\Debug\net481\NetBannerNG.exe");
#else
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NetBannerNG.exe");
#endif
        }
#pragma warning restore IDE0022 // Use expression body for method

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
                    if (!LaunchedProcesses.TryGetValue(process.Id, out var launchInfo))
                    {
                        return false;
                    }

                    if (SafeGetStartTimeUtc(process) != launchInfo.StartTimeUtc)
                    {
                        return false;
                    }

                    var commandLine = TryGetCommandLine(process.Id);
                    if (string.IsNullOrWhiteSpace(commandLine))
                    {
                        return false;
                    }

                    return HasExpectedPipeArgument(commandLine, launchInfo.PipeName);
                }
            }
            catch (Exception ex)
            {
                Program.Log.LogInformation(EventLogCatalog.ProcessIdentityValidationFailed, process.Id, ex.GetType().Name);
                return false;
            }
        }

        private static void TrackLaunchedProcess(Process process, string pipeName)
        {
            lock (LaunchSync)
            {
                LaunchedProcesses[process.Id] = new LaunchedProcessInfo
                {
                    StartTimeUtc = SafeGetStartTimeUtc(process),
                    PipeName = pipeName
                };
            }
        }

        private static HashSet<int> CaptureCandidateProcessIds(int interactiveSessionId) => Process.GetProcessesByName(ChildProcessName)
                .Where(p => p.SessionId == interactiveSessionId)
                .Select(p => p.Id)
                .ToHashSet();

        private static void TrackNewlyLaunchedProcesses(int interactiveSessionId, HashSet<int> existingCandidates, string pipeName)
        {
            foreach (var process in Process.GetProcessesByName(ChildProcessName).Where(p => p.SessionId == interactiveSessionId && !existingCandidates.Contains(p.Id)))
            {
                TrackLaunchedProcess(process, pipeName);
            }
        }

        private static void UntrackLaunchedProcess(int processId)
        {
            lock (LaunchSync)
            {
                LaunchedProcesses.Remove(processId);
            }
        }

        private static DateTime SafeGetStartTimeUtc(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime();
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static string? TryGetCommandLine(int processId)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
                foreach (var process in searcher.Get().Cast<ManagementObject>())
                {
                    return process["CommandLine"] as string;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        internal static bool HasExpectedPipeArgument(string? commandLine, string expectedPipeName)
        {
            if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(expectedPipeName))
            {
                return false;
            }

            return commandLine!.IndexOf($"--pipe={expectedPipeName}", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
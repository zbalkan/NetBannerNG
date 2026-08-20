using System.ComponentModel;
using System.Diagnostics;
using NetBannerNG.Common;
using NetBannerNG.Common.Extensions;
using NetBannerNG.Common.NamedPipes;

namespace NetBannerNG.Watchdog
{
    internal static class ProcessHelper
    {
        private const string ChildProcessName = "NetBannerNG";

        private sealed class LaunchedProcessInfo
        {
            public DateTime? StartTimeUtc { get; set; }
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
#pragma warning disable CA1031 // Do not catch general exception types
                Process? process = null;
                try
                {
                    process = Process.Start(psi);
                    if (process == null)
                    {
                        Program.Log.LogWarning(EventLogCatalog.ProcessStartFailed, psi.FileName, "Process.Start returned no process handle.");
                        return false;
                    }

                    if (!TrackLaunchedProcess(process, pipeName))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            Program.Log.LogWarning(EventLogCatalog.ProcessFailedToKill, process.Id, ex.GetMessageStack());
                        }

                        Program.Log.LogWarning(EventLogCatalog.ProcessStartFailed, psi.FileName, "Process started but could not be tracked.");
                        return false;
                    }

                    Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccessfully, psi.FileName);
                    return true;
                }
                catch (Exception ex)
                {
                    Program.Log.LogError(EventLogCatalog.ProcessStartFailed, psi.FileName, ex);
                    return false;
                }
                finally
                {
                    process?.Dispose();
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            if (!psi.RunAsActiveUser(out var processId, out var failedStep, out var win32Error))
            {
                var nativeMessage = new Win32Exception(win32Error).Message;
                Program.Log.LogError(EventLogCatalog.ProcessRunAsActiveUserFailed, psi.FileName, failedStep, win32Error, nativeMessage);
                return false;
            }

            if (!TrackLaunchedProcess(processId, pipeName))
            {
                Program.Log.LogWarning(EventLogCatalog.ProcessStartFailed, psi.FileName, $"Created process PID={processId} could not be tracked.");
                return false;
            }

            Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccessfully, psi.FileName);
            return true;
        }

        public static void KillAllChildProcess()
        {
            foreach (var process in GetChildProcesses())
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    process.Kill();
                    UntrackLaunchedProcess(process.Id);
                }
                catch (Exception ex)
                {
                    Program.Log.LogWarning(EventLogCatalog.ProcessFailedToKill, process.Id, ex.GetMessageStack());
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        public static bool IsChildProcessRunning()
        {
            var children = GetChildProcesses();
            foreach (var p in children)
            {
                p.Dispose();
            }

            return children.Count > 0;
        }

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

        private static List<Process> GetChildProcesses()
        {
            List<int> trackedProcessIds;
            lock (LaunchSync)
            {
                trackedProcessIds = LaunchedProcesses.Keys.ToList();
            }

            if (trackedProcessIds.Count == 0)
            {
                return new List<Process>();
            }

            var interactiveSessionId = (int)PrivilegeHelper.GetInteractiveSessionId();
            var candidates = new List<Process>(trackedProcessIds.Count);
            var staleProcessIds = new List<int>();
            foreach (var processId in trackedProcessIds)
            {
                try
                {
                    candidates.Add(Process.GetProcessById(processId));
                }
                catch (ArgumentException)
                {
                    staleProcessIds.Add(processId);
                }
                catch (InvalidOperationException)
                {
                    staleProcessIds.Add(processId);
                }
            }

            foreach (var processId in staleProcessIds)
            {
                UntrackLaunchedProcess(processId);
            }

            var result = new List<Process>(candidates.Count);
            foreach (var process in candidates)
            {
                if (IsExpectedChildProcess(process, interactiveSessionId))
                {
                    result.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }

            return result;
        }

        private static bool IsExpectedChildProcess(Process process, int interactiveSessionId)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                if (process.SessionId != interactiveSessionId)
                {
                    return false;
                }

                // Avoid Process.MainModule access here; cross-session and transient process states can
                // throw Win32Exception (e.g., partial ReadProcessMemory) and cause noisy failures.
                // Identity is validated using tracked PID + start time + expected --pipe argument.
                if (!string.Equals(process.ProcessName, ChildProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                lock (LaunchSync)
                {
                    if (!LaunchedProcesses.TryGetValue(process.Id, out var launchInfo))
                    {
                        return false;
                    }

                    var processStartTime = SafeGetStartTimeUtc(process);
                    if (processStartTime is null || launchInfo.StartTimeUtc is null || processStartTime.Value != launchInfo.StartTimeUtc.Value)
                    {
                        return false;
                    }

                    // The PID is returned directly by CreateProcessAsUser (or Process.Start
                    // in interactive mode). Session, process name, and start time protect
                    // against PID reuse without relying on a WMI command-line query.
                    return true;
                }
            }
            catch (Exception ex)
            {
                Program.Log.LogInformation(EventLogCatalog.ProcessIdentityValidationFailed, process.Id, ex.GetType().Name);
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private static bool TrackLaunchedProcess(Process process, string pipeName)
        {
            var startTimeUtc = SafeGetStartTimeUtc(process);
            if (startTimeUtc is null)
            {
                return false;
            }

            lock (LaunchSync)
            {
                LaunchedProcesses[process.Id] = new LaunchedProcessInfo
                {
                    StartTimeUtc = startTimeUtc,
                    PipeName = pipeName
                };
            }

            return true;
        }

        private static bool TrackLaunchedProcess(int processId, string pipeName)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return TrackLaunchedProcess(process, pipeName);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void UntrackLaunchedProcess(int processId)
        {
            lock (LaunchSync)
            {
                LaunchedProcesses.Remove(processId);
            }
        }

        private static DateTime? SafeGetStartTimeUtc(Process process)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                return process.StartTime.ToUniversalTime();
            }
            catch
            {
                return null;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }


        internal static bool HasExpectedPipeArgument(string? commandLine, string expectedPipeName) => !string.IsNullOrWhiteSpace(commandLine) && !string.IsNullOrWhiteSpace(expectedPipeName)
                && commandLine!.IndexOf($"--pipe={expectedPipeName}", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
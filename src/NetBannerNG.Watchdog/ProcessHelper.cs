using System.ComponentModel;
using System.Diagnostics;
using System.Management;
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
            public int Pid { get; set; }
            public DateTime? StartTimeUtc { get; set; }
            public string PipeName { get; set; } = string.Empty;
            public string? CommandLine { get; set; }
            public DateTime CommandLineLastAttemptUtc { get; set; } = DateTime.MinValue;
            public int CommandLineAttemptCount { get; set; }
        }

        // Keyed by session ID; one entry per active GUI session.
        private static readonly Dictionary<uint, LaunchedProcessInfo> SessionProcesses = new();
        private static readonly object LaunchSync = new();

        public static bool InitiateChildProcess(uint sessionId)
        {
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
                    if (process != null)
                    {
                        TrackSessionProcess(sessionId, process, pipeName);
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

            // N2/N3: Try to reattach to an existing GUI process before spawning a new one.
            if (TryReattachSessionProcess(sessionId, pipeName))
            {
                var existingPid = GetTrackedPidForSession(sessionId);
                Program.Log.LogInformation(EventLogCatalog.GuiSpawnedInSession, sessionId, existingPid);
                return true;
            }

            var existingCandidates = CaptureCandidateProcessIds((int)sessionId);
            if (!psi.RunAsSpecificUser(sessionId, out var failedStep, out var win32Error))
            {
                var nativeMessage = new Win32Exception(win32Error).Message;
                Program.Log.LogError(EventLogCatalog.ProcessRunAsActiveUserFailed, psi.FileName, sessionId, failedStep, win32Error, nativeMessage);
                return false;
            }

            if (!TrackNewlyLaunchedProcesses(sessionId, existingCandidates, pipeName))
            {
                Program.Log.LogWarning(EventLogCatalog.ProcessStartFailed, psi.FileName, $"Process launch command succeeded but no child process was observed in session {sessionId}.");
                return false;
            }

            var pid = GetTrackedPidForSession(sessionId);
            Program.Log.LogInformation(EventLogCatalog.GuiSpawnedInSession, sessionId, pid);
            Program.Log.LogInformation(EventLogCatalog.ProcessStartedSuccessfully, psi.FileName);
            return true;
        }

        public static void KillChildProcessInSession(uint sessionId)
        {
            var process = GetChildProcessForSession(sessionId);
            if (process == null)
            {
                return;
            }

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                process.Kill();
                UntrackSessionProcess(sessionId);
                Program.Log.LogInformation(EventLogCatalog.GuiTerminatedInSession, sessionId);
            }
            catch (Exception ex)
            {
                Program.Log.LogWarning(EventLogCatalog.ProcessFailedToKill, process.Id, ex.GetMessageStack());
            }
            finally
            {
                process.Dispose();
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public static void KillAllChildProcess()
        {
            List<uint> sessionIds;
            lock (LaunchSync)
            {
                sessionIds = SessionProcesses.Keys.ToList();
            }

            foreach (var sessionId in sessionIds)
            {
                KillChildProcessInSession(sessionId);
            }
        }

        public static bool IsChildProcessRunning(uint sessionId)
        {
            var process = GetChildProcessForSession(sessionId);
            if (process == null)
            {
                return false;
            }

            process.Dispose();
            return true;
        }

        private static Process? GetChildProcessForSession(uint sessionId)
        {
            int pid;
            LaunchedProcessInfo? launchInfo;
            lock (LaunchSync)
            {
                if (!SessionProcesses.TryGetValue(sessionId, out launchInfo))
                {
                    return null;
                }

                pid = launchInfo.Pid;
            }

            Process? process;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                UntrackSessionProcess(sessionId);
                return null;
            }
            catch (InvalidOperationException)
            {
                UntrackSessionProcess(sessionId);
                return null;
            }

            if (IsExpectedChildProcess(process, sessionId, launchInfo!))
            {
                return process;
            }

            process.Dispose();
            return null;
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

        private static bool IsExpectedChildProcess(Process process, uint expectedSessionId, LaunchedProcessInfo launchInfo)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                if ((uint)process.SessionId != expectedSessionId)
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

                var processStartTime = SafeGetStartTimeUtc(process);
                if (processStartTime is null || launchInfo.StartTimeUtc is null || processStartTime.Value != launchInfo.StartTimeUtc.Value)
                {
                    return false;
                }

                var commandLine = launchInfo.CommandLine;
                if (commandLine == null)
                {
                    var now = DateTime.UtcNow;
                    if (now - launchInfo.CommandLineLastAttemptUtc > TimeSpan.FromSeconds(1))
                    {
                        launchInfo.CommandLineLastAttemptUtc = now;
                        commandLine = TryGetCommandLine(process.Id);
                        launchInfo.CommandLine = commandLine;
                    }
                }

                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    // Retry command-line interrogation for a bounded number of attempts.
                    // This balances correctness with watchdog stability when WMI access is transiently unavailable.
                    if (launchInfo.CommandLineAttemptCount < 3)
                    {
                        launchInfo.CommandLineAttemptCount++;
                        Program.Log.LogWarning(EventLogCatalog.ProcessCommandLineUnavailable, process.Id);
                        return false;
                    }

                    // After bounded retries, accept tracked process identity based on PID/start-time/session/name.
                    Program.Log.LogWarning(EventLogCatalog.ProcessCommandLineUnavailable, process.Id);
                    return true;
                }

                return HasExpectedPipeArgument(commandLine, launchInfo.PipeName);
            }
            catch (Exception ex)
            {
                Program.Log.LogInformation(EventLogCatalog.ProcessIdentityValidationFailed, process.Id, ex.GetType().Name);
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private static void TrackSessionProcess(uint sessionId, Process process, string pipeName)
        {
            lock (LaunchSync)
            {
                SessionProcesses[sessionId] = new LaunchedProcessInfo
                {
                    Pid = process.Id,
                    StartTimeUtc = SafeGetStartTimeUtc(process),
                    PipeName = pipeName,
                    CommandLine = $"--pipe={pipeName}"
                };
            }
        }

        private static void UntrackSessionProcess(uint sessionId)
        {
            lock (LaunchSync)
            {
                SessionProcesses.Remove(sessionId);
            }
        }

        private static int GetTrackedPidForSession(uint sessionId)
        {
            lock (LaunchSync)
            {
                return SessionProcesses.TryGetValue(sessionId, out var info) ? info.Pid : -1;
            }
        }

        private static bool TryReattachSessionProcess(uint sessionId, string pipeName)
        {
            foreach (var process in Process.GetProcessesByName(ChildProcessName))
            {
                try
                {
                    if ((uint)process.SessionId == sessionId)
                    {
                        TrackSessionProcess(sessionId, process, pipeName);
                        return true;
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }

            return false;
        }

        private static HashSet<int> CaptureCandidateProcessIds(int sessionId)
        {
            var result = new HashSet<int>();
            foreach (var p in Process.GetProcessesByName(ChildProcessName))
            {
                if (p.SessionId == sessionId)
                {
                    result.Add(p.Id);
                }
                p.Dispose();
            }

            return result;
        }

        private static bool TrackNewlyLaunchedProcesses(uint sessionId, HashSet<int> existingCandidates, string pipeName)
        {
            var deadlineUtc = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow <= deadlineUtc)
            {
                foreach (var process in Process.GetProcessesByName(ChildProcessName))
                {
                    try
                    {
                        if ((uint)process.SessionId == sessionId && !existingCandidates.Contains(process.Id))
                        {
                            TrackSessionProcess(sessionId, process, pipeName);
                            return true;
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                if (ServiceHost.IsStopRequested)
                {
                    return false;
                }

                Thread.Sleep(50);
            }

            return false;
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

        private static string? TryGetCommandLine(int processId)
        {
#pragma warning disable CA1031 // Do not catch general exception types
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
#pragma warning restore CA1031 // Do not catch general exception types

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

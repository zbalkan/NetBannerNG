using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace NetBannerNG.Watchdog
{
    internal sealed class EventLogManager
    {
        private const string LogName = "Application";
        private const string SourceName = "NetBannerNG";
        private const int PendingQueueLimit = 256;
        private const int RetryDelaySeconds = 30;

        private const string SourceRegistryPath =
            $@"SYSTEM\CurrentControlSet\Services\EventLog\{LogName}\{SourceName}";

        private static readonly ConcurrentQueue<PendingEntry> _pending = new();
        private static int _pendingCount;
        private static volatile bool _ready;
        private static int _initLock;
        private static long _nextRetryTicks;
        private static int _overflowWarned;

        /// <summary>
        /// Call during service startup to eagerly register the event source before first write.
        /// </summary>
        public static void Initialize()
        {
            if (_ready)
            {
                return;
            }

#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                RegisterSource();
                _ready = true;
                FlushPending();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[NetBannerNG] Event log source registration failed: {ex.Message}");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public void LogInformation(string message) =>
            Write(EventLogEntryType.Information, message, 0);

        public void LogInformation(int eventId, string message) =>
            Write(EventLogEntryType.Information, message, eventId);

        public void LogInformation(EventDefinition definition, params object[] args) =>
            Write(EventLogEntryType.Information, definition.Format(args), definition.EventId);

        public void LogWarning(string message) =>
            Write(EventLogEntryType.Warning, message, 0);

        public void LogWarning(int eventId, string message) =>
            Write(EventLogEntryType.Warning, message, eventId);

        public void LogWarning(EventDefinition definition, params object[] args) =>
            Write(EventLogEntryType.Warning, definition.Format(args), definition.EventId);

        public void LogDebug(string message) =>
            Trace.WriteLine($"DEBUG {message}");

        public void LogError(string message) =>
            Write(EventLogEntryType.Error, message, 0);

        public void LogError(int eventId, string message) =>
            Write(EventLogEntryType.Error, message, eventId);

        public void LogError(EventDefinition definition, params object[] args) =>
            Write(EventLogEntryType.Error, definition.Format(args), definition.EventId);

        private static void Write(EventLogEntryType type, string message, int eventId)
        {
            WriteToTrace(type, message);

            if (!_ready)
            {
                ScheduleInitialization();
            }

            var entry = new PendingEntry(type, message, eventId);
            if (_ready)
            {
                if (!TryWriteEntry(entry))
                {
                    Enqueue(entry);
                }

                return;
            }

            Enqueue(entry);
        }

        private static void ScheduleInitialization()
        {
            if (Interlocked.Read(ref _nextRetryTicks) > DateTime.UtcNow.Ticks)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _initLock, 1, 0) != 0)
            {
                return;
            }

            Task.Run(() => {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    RegisterSource();
                    _ready = true;
                    FlushPending();
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"[NetBannerNG] Event log initialization failed: {ex.Message}");
                    _ready = false;
                    Interlocked.Exchange(ref _nextRetryTicks, DateTime.UtcNow.AddSeconds(RetryDelaySeconds).Ticks);
                }
                finally
                {
                    Interlocked.Exchange(ref _initLock, 0);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            });
        }

        private static void RegisterSource()
        {
            var messageFile = ResolveMessageResourceFile();

            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(new EventSourceCreationData(SourceName, LogName)
                {
                    MessageResourceFile = messageFile,
                });
            }
            else
            {
                RepairMessageResourceFile(messageFile);
            }

            var registered = EventLog.LogNameFromSourceName(SourceName, ".");
            if (!string.Equals(registered, LogName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Event source '{SourceName}' is registered to '{registered}', expected '{LogName}'.");
            }
        }

        // The .NET Framework EventLogMessages.dll ships with the runtime and contains
        // 65,536 "%1" message templates, one per event ID 0..65535. Pointing the source's
        // EventMessageFile at it lets Event Viewer render WriteEntry's text verbatim for
        // any event ID we use. Without this, Event Viewer reports "the description for
        // Event ID ... cannot be found", and IDs that collide with Win32 error codes get
        // the unrelated kernel32 text (e.g. 3010 -> "A system reboot is required").
        private static string ResolveMessageResourceFile()
        {
            var runtimePath = Path.Combine(
                RuntimeEnvironment.GetRuntimeDirectory(),
                "EventLogMessages.dll");
            if (File.Exists(runtimePath))
            {
                return runtimePath;
            }

            return Path.Combine(
                Environment.ExpandEnvironmentVariables("%SystemRoot%"),
                @"Microsoft.NET\Framework64\v4.0.30319\EventLogMessages.dll");
        }

        // The installer registers the source with the correct EventMessageFile, but a
        // prior install (or a foreign WriteEntry call that auto-registered the source)
        // may have left a value that points nowhere. Repair it when we have write access;
        // LocalService doesn't, and that's fine — the install-time entry is authoritative.
        private static void RepairMessageResourceFile(string desiredPath)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(SourceRegistryPath, writable: true);
                if (key is null)
                {
                    return;
                }

                var current = key.GetValue(
                    "EventMessageFile",
                    defaultValue: null,
                    options: RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                var expanded = string.IsNullOrEmpty(current)
                    ? null
                    : Environment.ExpandEnvironmentVariables(current);
                if (!string.IsNullOrEmpty(expanded) && File.Exists(expanded))
                {
                    return;
                }

                key.SetValue("EventMessageFile", desiredPath, RegistryValueKind.ExpandString);
                key.SetValue("TypesSupported", 7, RegistryValueKind.DWord);
            }
            catch (SecurityException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[NetBannerNG] Event source repair failed: {ex.Message}");
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private static void FlushPending()
        {
            while (_ready && _pending.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _pendingCount);
                if (!TryWriteEntry(entry))
                {
                    Enqueue(entry);
                    break;
                }
            }
        }

        private static void Enqueue(PendingEntry entry)
        {
            _pending.Enqueue(entry);
            var count = Interlocked.Increment(ref _pendingCount);
            if (count <= PendingQueueLimit)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _overflowWarned, 1, 0) == 0)
            {
                Trace.TraceWarning($"[NetBannerNG] Event log queue overflow: oldest entries will be dropped (limit={PendingQueueLimit}).");
            }

            if (_pending.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _pendingCount);
            }
        }

        private static bool TryWriteEntry(PendingEntry entry)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                EventLog.WriteEntry(SourceName, entry.Message, entry.Type, entry.EventId);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[NetBannerNG] Event log write failed: {ex.Message}");
                _ready = false;
                ScheduleInitialization();
                return false;
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        private static void WriteToTrace(EventLogEntryType type, string message)
        {
            switch (type)
            {
                case EventLogEntryType.Warning:
                    Trace.TraceWarning(message);
                    break;

                case EventLogEntryType.Error:
                    Trace.TraceError(message);
                    break;

                default:
                    Trace.TraceInformation(message);
                    break;
            }
        }

        internal readonly struct PendingEntry
        {
            public EventLogEntryType Type { get; }
            public string Message { get; }
            public int EventId { get; }

            public PendingEntry(EventLogEntryType type, string message, int eventId)
            {
                Type = type;
                Message = message;
                EventId = eventId;
            }
        }
    }
}
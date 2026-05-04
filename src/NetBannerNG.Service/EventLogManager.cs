using System.Collections.Concurrent;
using System.Diagnostics;

namespace NetBannerNG.Service
{
    internal sealed class EventLogManager : ILogManager
    {
        private const string LogName = "Application";
        private const string SourceName = "NetBannerNG";
        private const int PendingQueueLimit = 256;
        private const int RetryDelaySeconds = 30;

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
        }

        public void LogInformation(string message) =>
            Write(EventLogEntryType.Information, message);

        public void LogWarning(string message) =>
            Write(EventLogEntryType.Warning, message);

        public void LogDebug(string message) =>
            Trace.WriteLine($"DEBUG {message}");

        public void LogError(string message) =>
            Write(EventLogEntryType.Error, message);

        private static void Write(EventLogEntryType type, string message)
        {
            WriteToTrace(type, message);

            if (!_ready)
            {
                ScheduleInitialization();
            }

            var entry = new PendingEntry(type, message);
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

            Task.Run(() =>
            {
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
            });
        }

        private static void RegisterSource()
        {
            if (!EventLog.SourceExists(SourceName))
            {
                EventLog.CreateEventSource(new EventSourceCreationData(SourceName, LogName));
            }

            var registered = EventLog.LogNameFromSourceName(SourceName, ".");
            if (!string.Equals(registered, LogName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Event source '{SourceName}' is registered to '{registered}', expected '{LogName}'.");
            }
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
            try
            {
                EventLog.WriteEntry(SourceName, entry.Message, entry.Type);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[NetBannerNG] Event log write failed: {ex.Message}");
                _ready = false;
                ScheduleInitialization();
                return false;
            }
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

        public struct PendingEntry : IEquatable<PendingEntry>
        {
            public EventLogEntryType Type { get; }
            public string Message { get; }

            public PendingEntry(EventLogEntryType type, string message)
            {
                Type = type;
                Message = message;
            }

            // Manual Value Equality
            public readonly bool Equals(PendingEntry other) =>
                Type == other.Type && Message == other.Message;

            public override bool Equals(object obj) =>
                obj is PendingEntry other && Equals(other);

            public override readonly int GetHashCode()
            {
                // Using the BCL HashCode package we discussed!
                var hash = new HashCode();
                hash.Add(Type);
                hash.Add(Message);
                return hash.ToHashCode();
            }

            public static bool operator ==(PendingEntry left, PendingEntry right) => left.Equals(right);
            public static bool operator !=(PendingEntry left, PendingEntry right) => !left.Equals(right);
        }
    }
}

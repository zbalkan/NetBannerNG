using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NetBannerNG.Common;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Common.Native;
using NetBannerNG.Services;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    internal static class WindowWatcher
    {
        private const int EventObjectLocationChange = 0x800B;
        private const int EventSystemForeground = 0x0003;
        private const int ObjectIdWindow = 0;
        private static readonly NativeTypes.WinEventHook HookProc = HookCallback;
        private static readonly object HookSync = new();
        private static readonly Dictionary<string, (bool IsSuppressed, string AppName)> LastSuppressionStateByGroup = new(StringComparer.Ordinal);
        private static readonly object SuppressionStateSync = new();
        private static readonly object WindowCacheSync = new();
        private static readonly Dictionary<IntPtr, (MonitorRect Rect, long Ticks)> WindowRectCache = new();
        private static long _windowCachePruneChecks;
        private static long _lastWindowCachePruneAtTicks;
        private const long WindowCacheTtlTicks = TimeSpan.TicksPerMillisecond * 200;
        private const int WindowCachePruneEveryNMisses = 32;
        private const long WindowCachePruneCadenceTicks = TimeSpan.TicksPerMillisecond * 750;
        private const int WindowCacheMaxEntries = 512;
        private static IntPtr _foregroundHookId;
        private static long _lastReEvaluatedAtTicks;
        private static IntPtr _locationHookId;
        private static long _previousForegroundWindowValue;
        internal static event Action<IReadOnlyDictionary<string, FullscreenSuppressionState>>? FullscreenSuppressionUpdated;

        internal static Func<string, Task>? EventLogSinkAsync { get; set; }
        private static IntPtr DesktopHandle => User32.GetDesktopWindow(); //Window handle for the desktop
        private static IntPtr ShellHandle => User32.GetShellWindow(); //Window handle for the shell
        private static IntPtr TaskbarHandle => User32.FindWindow("Shell_TrayWnd", null!);
        internal static void Unwatch()
        {
            lock (HookSync)
            {
                if (_foregroundHookId != default) { _ = User32.UnhookWinEvent(_foregroundHookId); _foregroundHookId = default; }
                if (_locationHookId != default) { _ = User32.UnhookWinEvent(_locationHookId); _locationHookId = default; }
            }
            // Hook callbacks can race while the UI loop is draining; keep state-map resets serialized.
            lock (SuppressionStateSync) { LastSuppressionStateByGroup.Clear(); }
        }

        internal static void Watch()
        {
            lock (HookSync)
            {
                if (_foregroundHookId == default)
                {
                    _foregroundHookId = SetHook(EventSystemForeground, EventSystemForeground);
                }

                if (_locationHookId == default)
                {
                    // Catches in-place fullscreen transitions (e.g. Chrome F11) that do not
                    // change the foreground window.
                    _locationHookId = SetHook(EventObjectLocationChange, EventObjectLocationChange);
                }
            }
        }
        private static void ApplyPerMonitorFullscreenSuppression()
        {
            var monitors = Monitor.AllMonitors.ToList();
            var ownWindowHandles = SnapshotOwnWindowHandles();
            var windows = SnapshotWindowsInZOrder();
            var fullscreenByGroup = FullscreenSuppressionEvaluator.EvaluateByGroup(monitors, ownWindowHandles, windows);
            var fullscreenAppByGroup = ResolveFullscreenAppsByGroup(monitors, ownWindowHandles, windows);

            Debug.WriteLine($"[Fullscreen][Scan] Monitors={monitors.Count} OwnWindows={ownWindowHandles.Count} WindowsScanned={windows.Count}");
            BeginOnUi(() => {
                var suppressionStateByGroup = fullscreenByGroup.ToDictionary(
                    pair => pair.Key,
                    pair => new FullscreenSuppressionState(pair.Value, fullscreenAppByGroup.TryGetValue(pair.Key, out var appName) ? appName : null),
                    StringComparer.Ordinal);
                FullscreenSuppressionUpdated?.Invoke(suppressionStateByGroup);
                foreach (var monitor in monitors)
                {
                    var groupId = MonitorIdentity.BuildGroupId(monitor);
                    var isFullscreen = fullscreenByGroup.TryGetValue(groupId, out var fullscreen) && fullscreen;
                    var appName = fullscreenAppByGroup.TryGetValue(groupId, out var app) ? app : "Unknown";
                    Debug.WriteLine($"[Fullscreen][Apply] Group={groupId} Monitor={monitor.Bounds} IsFullscreen={isFullscreen}");
                    LogSuppressionStateTransition(groupId, monitor.Bounds.ToString(CultureInfo.InvariantCulture), isFullscreen, appName);
                }
            });
        }

        private static void BeginOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _ = dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }

        private static string GetClassNameSafe(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);
            return User32.GetClassName(hwnd, builder, builder.Capacity) > 0 ? builder.ToString() : string.Empty;
        }

        private static MonitorRect GetWindowBounds(IntPtr current)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            const int dwmaExtendedFrameBounds = 9;
            lock (WindowCacheSync)
            {
                if (WindowRectCache.TryGetValue(current, out var cached) && nowTicks - cached.Ticks <= WindowCacheTtlTicks)
                {
                    return cached.Rect;
                }
            }

            MonitorRect bounds;
            if (DwmApi.DwmGetWindowAttribute(current, dwmaExtendedFrameBounds, out MonitorRect dwmBounds, System.Runtime.InteropServices.Marshal.SizeOf<MonitorRect>()) == 0)
            {
                bounds = dwmBounds;
            }
            else
            {
                _ = User32.GetWindowRect(current, out var appBounds);
                bounds = appBounds;
            }
            lock (WindowCacheSync)
            {
                _windowCachePruneChecks++;
                if (_windowCachePruneChecks % WindowCachePruneEveryNMisses == 0
                    || nowTicks - _lastWindowCachePruneAtTicks >= WindowCachePruneCadenceTicks
                    || WindowRectCache.Count >= WindowCacheMaxEntries)
                {
                    PruneWindowCacheUnderLock(nowTicks);
                }
                WindowRectCache[current] = (bounds, nowTicks);
            }

            return bounds;
        }

        private static void HookCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != ObjectIdWindow || idChild != 0 || hWnd == IntPtr.Zero)
            {
                return;
            }

            if (eventType == EventSystemForeground)
            {
                var fg = User32.GetForegroundWindow().ToInt64();
                if (Interlocked.Exchange(ref _previousForegroundWindowValue, fg) == fg)
                {
                    return;
                }
            }
            else if (eventType == EventObjectLocationChange)
            {
                // Filter the firehose: only react to size/position changes on the active window,
                // debounced. Drop the cached bounds entry so the re-eval samples the post-change rect.
                if (hWnd != User32.GetForegroundWindow())
                {
                    return;
                }

                const long debounceTicks = TimeSpan.TicksPerMillisecond * 150;
                var now = DateTime.UtcNow.Ticks;
                if (now - Interlocked.Read(ref _lastReEvaluatedAtTicks) < debounceTicks)
                {
                    return;
                }

                Interlocked.Exchange(ref _lastReEvaluatedAtTicks, now);
                lock (WindowCacheSync) { WindowRectCache.Remove(hWnd); }
            }
            else
            {
                return;
            }

            ApplyPerMonitorFullscreenSuppression();
        }

        // Single eligibility predicate for fullscreen-candidate windows. A window is a candidate
        // when it is a real, visible, on-screen top-level window owned by another process — i.e.
        // not the desktop, the shell, the taskbar (or their well-known auxiliary windows), not
        // minimized, and not DWM-cloaked (e.g. backgrounded UWP).
        private static bool IsFullscreenCandidate(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || hwnd == DesktopHandle || hwnd == ShellHandle || hwnd == TaskbarHandle)
            {
                return false;
            }

            var className = GetClassNameSafe(hwnd);
            if (string.Equals(className, "Progman", StringComparison.Ordinal)
                || string.Equals(className, "WorkerW", StringComparison.Ordinal)
                || string.Equals(className, "SHELLDLL_DefView", StringComparison.Ordinal)
                || string.Equals(className, "Shell_TrayWnd", StringComparison.Ordinal)
                || string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.Ordinal))
            {
                Debug.WriteLine($"[Fullscreen][Ignore] HWND={hwnd} Reason=ShellOrDesktop Class={className}");
                return false;
            }

            if (!User32.IsWindowVisible(hwnd) || User32.IsIconic(hwnd))
            {
                return false;
            }

            const int dwmaCloaked = 14;
            if (DwmApi.DwmGetWindowAttribute(hwnd, dwmaCloaked, out int isCloaked, sizeof(int)) == 0 && isCloaked != 0)
            {
                return false;
            }

            return true;
        }

        private static void LogSuppressionStateTransition(string groupId, string monitorBounds, bool isSuppressed, string appName)
        {
            var previousAppName = string.Empty;
            // Transition logs may be emitted from rapid callback bursts; keep check/update atomic per group.
            lock (SuppressionStateSync)
            {
                if (LastSuppressionStateByGroup.TryGetValue(groupId, out var lastState) && lastState.IsSuppressed == isSuppressed)
                {
                    return;
                }
                previousAppName = lastState.AppName;
                LastSuppressionStateByGroup[groupId] = (isSuppressed, appName);
            }

            var message = isSuppressed
                ? $"[FullscreenSuppression] Group={groupId} Monitor={monitorBounds} State=Suppressed FullscreenApp={appName} Behavior=Bars stay behind fullscreen app."
                : $"[FullscreenSuppression] Group={groupId} Monitor={monitorBounds} State=Normal FullscreenApp={(!string.IsNullOrWhiteSpace(previousAppName) ? previousAppName : appName)} Behavior=Fullscreen closed; bars restored.";
            _ = EventLogSinkAsync?.Invoke(message);
        }

        private static void PruneWindowCacheUnderLock(long nowTicks)
        {
            _lastWindowCachePruneAtTicks = nowTicks;
            List<IntPtr>? expiredEntries = null;
            foreach (var kv in WindowRectCache)
            {
                if (nowTicks - kv.Value.Ticks > WindowCacheTtlTicks)
                {
                    (expiredEntries ??= new List<IntPtr>()).Add(kv.Key);
                }
            }

            if (expiredEntries is not null)
            {
                foreach (var key in expiredEntries)
                {
                    WindowRectCache.Remove(key);
                }
            }

            if (WindowRectCache.Count <= WindowCacheMaxEntries)
            {
                return;
            }

            var overflow = WindowRectCache.Count - WindowCacheMaxEntries;
            foreach (var key in WindowRectCache.OrderBy(pair => pair.Value.Ticks).Take(overflow).Select(pair => pair.Key).ToList())
            {
                WindowRectCache.Remove(key);
            }
        }

        private static Dictionary<string, string> ResolveFullscreenAppsByGroup(IReadOnlyList<Monitor> monitors, HashSet<IntPtr> ownWindowHandles, IReadOnlyList<FullscreenSuppressionEvaluator.WindowSnapshot> windows)
        {
            var boundsGroupToActualGroup = monitors.ToDictionary(
                monitor => MonitorIdentity.BuildGroupId(string.Empty, monitor.Bounds),
                MonitorIdentity.BuildGroupId,
                StringComparer.Ordinal);
            var results = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var window in windows)
            {
                if (!window.IsVisible || ownWindowHandles.Contains(window.Handle))
                {
                    continue;
                }

                var boundsGroupId = MonitorIdentity.BuildGroupId(string.Empty, (Rect)window.MonitorBounds);
                if (!boundsGroupToActualGroup.TryGetValue(boundsGroupId, out var groupId) || results.ContainsKey(groupId))
                {
                    continue;
                }

                if (!FullscreenSuppressionEvaluator.IsFullscreen(window.Bounds, window.MonitorBounds))
                {
                    continue;
                }

                var processName = ResolveWindowProcessName(window.Handle);
                Debug.WriteLine($"[Fullscreen][Candidate] Group={groupId} HWND={window.Handle} Class={GetClassNameSafe(window.Handle)} Process={processName} Bounds={window.Bounds} Monitor={window.MonitorBounds}");
                results[groupId] = processName;
            }

            return results;
        }

        private static string ResolveWindowProcessName(IntPtr handle)
        {
            _ = User32.GetWindowThreadProcessId(handle, out var processId);
            if (processId == 0)
            {
                return "Unknown";
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
            catch (ArgumentException)
            {
                return $"PID:{processId}";
            }
            catch (InvalidOperationException)
            {
                return $"PID:{processId}";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return $"PID:{processId}";
            }
        }

        private static IntPtr SetHook(int eventMin, int eventMax) => User32.SetWinEventHook(
                                                                                    eventMin, eventMax, IntPtr.Zero, HookProc, 0, 0,
            (int)(NativeTypes.SetWinEventHook.SkipOwnProcess | NativeTypes.SetWinEventHook.OutOfContext));
        private static HashSet<IntPtr> SnapshotOwnWindowHandles()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return new HashSet<IntPtr>();
            }

            if (dispatcher.CheckAccess())
            {
                return Application.Current!.Windows.Cast<Window>().Select(window => window.GetHandle()).ToHashSet();
            }

            return dispatcher.Invoke(() => Application.Current!.Windows.Cast<Window>().Select(window => window.GetHandle()).ToHashSet());
        }

        private static List<FullscreenSuppressionEvaluator.WindowSnapshot> SnapshotWindowsInZOrder()
        {
            const uint gwHwndNext = 2;
            var windows = new List<FullscreenSuppressionEvaluator.WindowSnapshot>();
            var current = User32.GetTopWindow(IntPtr.Zero);
            while (current != IntPtr.Zero)
            {
                if (IsFullscreenCandidate(current))
                {
                    var monitorBounds = Monitor.GetMonitorBounds(current);
                    var windowBounds = GetWindowBounds(current);
                    windows.Add(new FullscreenSuppressionEvaluator.WindowSnapshot(current, windowBounds, monitorBounds, User32.IsWindowVisible(current)));
                }

                current = User32.GetWindow(current, gwHwndNext);
            }

            return windows;
        }
    }
}

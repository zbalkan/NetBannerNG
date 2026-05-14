using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Common.Native;
using Monitor = NetBannerNG.Common.Native.Monitor;

namespace NetBannerNG.Utils
{
    internal static class WindowWatcher
    {
        private const int EventSystemForeground = 0x0003;
        private const int ObjectIdWindow = 0;
        private static readonly object HookSync = new();

        private static readonly NativeMethods.WinEventHook ForegroundWindowHook = HookCallback;
        private static IntPtr DesktopHandle => NativeMethods.GetDesktopWindow(); //Window handle for the desktop
        private static IntPtr ShellHandle => NativeMethods.GetShellWindow(); //Window handle for the shell
        private static IntPtr TaskbarHandle => NativeMethods.FindWindow("Shell_TrayWnd", null!);
        private static long _previousForegroundWindowValue;
        private static readonly object WindowCacheSync = new();
        private static readonly Dictionary<IntPtr, (MonitorRect Rect, long Ticks)> WindowRectCache = new();
        private static IntPtr _hookId;

        internal static void Watch()
        {
            lock (HookSync)
            {
                if (_hookId != default)
                {
                    return;
                }

                _hookId = SetHook(ForegroundWindowHook);
            }
        }

        internal static void Unwatch()
        {
            lock (HookSync)
            {
                if (_hookId == default)
                {
                    return;
                }

                _ = NativeMethods.UnhookWinEvent(_hookId);
                _hookId = default;
            }

        }

        private static IntPtr SetHook(NativeMethods.WinEventHook hookProc) => NativeMethods.SetWinEventHook(
                EventSystemForeground,
                EventSystemForeground,
                IntPtr.Zero,
                hookProc,
                0,
                0,
                (int)(NativeMethods.SetWinEventHookFlags.SkipOwnProcess | NativeMethods.SetWinEventHookFlags.OutOfContext));

        private static void HookCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType != EventSystemForeground || idObject != ObjectIdWindow || idChild != 0 || hWnd == IntPtr.Zero)
            {
                return;
            }

            var foregroundWindowHandle = NativeMethods.GetForegroundWindow();
            var foregroundWindowValue = foregroundWindowHandle.ToInt64();
            if (Interlocked.Exchange(ref _previousForegroundWindowValue, foregroundWindowValue) == foregroundWindowValue)
            {
                return;
            }

            ApplyPerMonitorFullscreenSuppression();
        }

        private static bool IsValid(IntPtr windowHandle)
        {
            // If handle is default, there is a problem, return false.
            if (windowHandle.Equals(IntPtr.Zero))
            {
                return false;
            }

            // Check we haven't picked up the desktop or the shell
            if (windowHandle.Equals(DesktopHandle) || windowHandle.Equals(ShellHandle))
            {
                return false;
            }

            // Check if we picked the taskbar.
            if (windowHandle.Equals(TaskbarHandle))
            {
                return false;
            }

            return true;
        }

        private static void ApplyPerMonitorFullscreenSuppression()
        {
            var monitors = Monitor.AllMonitors.ToList();
            var ownWindowHandles = SnapshotOwnWindowHandles();
            var windows = SnapshotWindowsInZOrder();
            var fullscreenByGroup = FullscreenSuppressionEvaluator.EvaluateByGroup(monitors, ownWindowHandles, windows);

            Debug.WriteLine($"[Fullscreen][Scan] Monitors={monitors.Count} OwnWindows={ownWindowHandles.Count} WindowsScanned={windows.Count}");
            BeginOnUi(() => {
                foreach (var monitor in monitors)
                {
                    var groupId = BorderManager.BuildGroupId(monitor);
                    var isFullscreen = fullscreenByGroup.TryGetValue(groupId, out var fullscreen) && fullscreen;
                    Debug.WriteLine($"[Fullscreen][Apply] Group={groupId} Monitor={monitor.Bounds} IsFullscreen={isFullscreen}");
                    BorderManager.SetMonitorFullscreenSuppressedState(monitor, isFullscreen);
                }
            });
        }

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
            var current = NativeMethods.GetTopWindow(IntPtr.Zero);
            while (current != IntPtr.Zero)
            {
                if (IsValid(current))
                {
                    var monitorBounds = Monitor.GetMonitorBounds(current);
                    var windowBounds = GetWindowBounds(current);
                    windows.Add(new FullscreenSuppressionEvaluator.WindowSnapshot(current, windowBounds, monitorBounds, NativeMethods.IsWindowVisible(current)));
                }

                current = NativeMethods.GetWindow(current, gwHwndNext);
            }

            return windows;
        }

        private static MonitorRect GetWindowBounds(IntPtr current)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            const long cacheTicks = TimeSpan.TicksPerMillisecond * 200;
            const int dwmaExtendedFrameBounds = 9;
            lock (WindowCacheSync)
            {
                if (WindowRectCache.TryGetValue(current, out var cached) && nowTicks - cached.Ticks <= cacheTicks)
                {
                    return cached.Rect;
                }
            }

            MonitorRect bounds;
            if (NativeMethods.DwmGetWindowAttribute(current, dwmaExtendedFrameBounds, out var dwmBounds, System.Runtime.InteropServices.Marshal.SizeOf<MonitorRect>()) == 0)
            {
                bounds = dwmBounds;
            }
            else
            {
                _ = NativeMethods.GetWindowRect(current, out var appBounds);
                bounds = appBounds;
            }
            lock (WindowCacheSync)
            {
                WindowRectCache[current] = (bounds, nowTicks);
            }

            return bounds;
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
    }
}

using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using NetBannerNG.Common.AppBar;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Utils
{
    internal static class WindowWatcher
    {
        private const int EventSystemForeground = 0x0003;
        private const int ObjectIdWindow = 0;
        private static readonly TimeSpan ForegroundDispatchDebounce = TimeSpan.FromMilliseconds(125);

        private static readonly NativeMethods.WinEventHook ForegroundWindowHook = HookCallback;
        private static IntPtr DesktopHandle => NativeMethods.GetDesktopWindow(); //Window handle for the desktop
        private static IntPtr ShellHandle => NativeMethods.GetShellWindow(); //Window handle for the shell
        private static IntPtr TaskbarHandle => NativeMethods.FindWindow("Shell_TrayWnd", null!);
        private static IntPtr _previousForegroundWindowHandle;
        private static IntPtr _hookId;
        private static DateTime _lastDispatchUtc = DateTime.MinValue;
        private static bool _pendingSendTop;
        private static bool _pendingSendBottom;
        private static DispatcherTimer? _debounceTimer;

        internal static void Watch() => _hookId = SetHook(ForegroundWindowHook);

        internal static void Unwatch()
        {
            if (_hookId == default)
            {
                return;
            }

            _ = NativeMethods.UnhookWinEvent(_hookId);
            _hookId = default;
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
            if (foregroundWindowHandle == _previousForegroundWindowHandle)
            {
                return;
            }

            _previousForegroundWindowHandle = foregroundWindowHandle;

            if (!IsValid(foregroundWindowHandle))
            {
                return;
            }

            var windowBounds = GetWindowBounds(foregroundWindowHandle);

            var isFullScreen = IsFullScreen(foregroundWindowHandle);
            var monitorBounds = Monitor.GetMonitorBounds(foregroundWindowHandle);
            Debug.WriteLine($"Window handle: {foregroundWindowHandle} | Full screen: {isFullScreen} | Window Bounds: {windowBounds} | Monitor bounds: {monitorBounds}");

            if (isFullScreen)
            {
                DispatchCoalesced(sendBottom: true);
                //var newBounds = GetModifiedFullscreenBound(foregroundWindowHandle);
                //ResizeForegroundWindow(foregroundWindowHandle, newBounds);
                //BorderManager.InitiateAllBorders(true, true);
            }
            else
            {
                DispatchCoalesced(sendBottom: false);
            }
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

            // if window is one of ours, ignore the calculation
            return !Application.Current.Windows.Cast<Window>().Any(window => windowHandle.Equals(window.GetHandle()));
        }

        private static bool IsFullScreen(IntPtr current)
        {
            // Determine if the window is fullscreen
            var windowBounds = GetWindowBounds(current);
            var screenBounds = Monitor.GetMonitorBounds(current);
            const double tolerance = 2.0;
            return Math.Abs(windowBounds.Left - screenBounds.Left) <= tolerance
                   && Math.Abs(windowBounds.Top - screenBounds.Top) <= tolerance
                   && Math.Abs(windowBounds.Right - screenBounds.Right) <= tolerance
                   && Math.Abs(windowBounds.Bottom - screenBounds.Bottom) <= tolerance;
        }

        //private static MonitorRect GetModifiedFullscreenBound(IntPtr currentMonitorHandle)
        //{
        //    var screenBounds = Monitor.GetMonitorBounds(currentMonitorHandle);
        //    var bannerSize = Settings.Instance.BannerSize;
        //    var borderSize = Settings.Instance.BorderSize;

        //    var newSize = new MonitorRect
        //    {
        //        Top = screenBounds.Top + bannerSize,
        //        Bottom = screenBounds.Bottom - borderSize,
        //        Left = screenBounds.Left + borderSize,
        //        Right = screenBounds.Right - borderSize
        //    };

        //    return newSize;
        //}

        //private static void ResizeForegroundWindow(IntPtr targetWindowHandle, MonitorRect bounds)
        //{
        //    var x = bounds.Left;
        //    var y = bounds.Top;
        //    var width = bounds.Right - bounds.Left;
        //    var height = bounds.Bottom - bounds.Top;
        //    _ = NativeMethods.SetWindowPos(targetWindowHandle, IntPtr.Zero, x, y, width, height,
        //        NativeMethods.SetWindowPosFlags.Undefined);
        //}

        private static void DispatchCoalesced(bool sendBottom)
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastDispatchUtc;
            if (elapsed >= ForegroundDispatchDebounce)
            {
                _lastDispatchUtc = now;
                _ = Application.Current.Dispatcher.BeginInvoke(sendBottom ? BorderManager.SendBottom : BorderManager.SendTop, DispatcherPriority.Background);
                return;
            }

            _pendingSendBottom |= sendBottom;
            _pendingSendTop |= !sendBottom;
            var remaining = ForegroundDispatchDebounce - elapsed;
            StartOrResetDebounceTimer(remaining);
        }

        private static void StartOrResetDebounceTimer(TimeSpan dueIn)
        {
            _debounceTimer ??= new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
            _debounceTimer.Stop();
            _debounceTimer.Interval = dueIn;
            _debounceTimer.Tick -= OnDebounceTick;
            _debounceTimer.Tick += OnDebounceTick;
            _debounceTimer.Start();
        }

        private static void OnDebounceTick(object? sender, EventArgs e)
        {
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Tick -= OnDebounceTick;
            }

            var dispatchBottom = _pendingSendBottom;
            var dispatchTop = _pendingSendTop;
            _pendingSendBottom = false;
            _pendingSendTop = false;
            if (!dispatchBottom && !dispatchTop)
            {
                return;
            }

            _lastDispatchUtc = DateTime.UtcNow;
            if (dispatchBottom)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(BorderManager.SendBottom, DispatcherPriority.Background);
            }
            if (dispatchTop)
            {
                _ = Application.Current.Dispatcher.BeginInvoke(BorderManager.SendTop, DispatcherPriority.Background);
            }
        }

        private static Rect GetWindowBounds(IntPtr current)
        {
            _ = NativeMethods.GetWindowRect(current, out var appBounds);
            return (Rect)(appBounds);
        }
    }
}
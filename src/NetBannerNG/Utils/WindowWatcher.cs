using System;
using System.Diagnostics;
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
        private static readonly TimeSpan ForegroundDispatchDebounce = TimeSpan.FromMilliseconds(125);
        private static readonly long ForegroundDispatchDebounceTicks = ForegroundDispatchDebounce.Ticks;
        private static readonly object HookSync = new();

        private static readonly NativeMethods.WinEventHook ForegroundWindowHook = HookCallback;
        private static IntPtr DesktopHandle => NativeMethods.GetDesktopWindow(); //Window handle for the desktop
        private static IntPtr ShellHandle => NativeMethods.GetShellWindow(); //Window handle for the shell
        private static IntPtr TaskbarHandle => NativeMethods.FindWindow("Shell_TrayWnd", null!);
        private static long _previousForegroundWindowValue;
        private static IntPtr _hookId;
        private static long _lastDispatchTicks;
        private static int _pendingSendTop;
        private static int _pendingSendBottom;
        private static DispatcherTimer? _debounceTimer;

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

            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Tick -= OnDebounceTick;
                _debounceTimer = null;
            }

            Interlocked.Exchange(ref _pendingSendTop, 0);
            Interlocked.Exchange(ref _pendingSendBottom, 0);
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
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return false;
            }
            if (dispatcher.CheckAccess())
            {
                return !Application.Current!.Windows.Cast<Window>().Any(window => windowHandle.Equals(window.GetHandle()));
            }

            return !dispatcher.Invoke(() => Application.Current!.Windows.Cast<Window>().Any(window => windowHandle.Equals(window.GetHandle())));
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

        private static void DispatchCoalesced(bool sendBottom)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            while (true)
            {
                var lastTicks = Interlocked.Read(ref _lastDispatchTicks);
                var elapsedTicks = nowTicks - lastTicks;
                if (elapsedTicks >= ForegroundDispatchDebounceTicks)
                {
                    if (Interlocked.CompareExchange(ref _lastDispatchTicks, nowTicks, lastTicks) != lastTicks)
                    {
                        continue;
                    }

                    BeginOnUi(sendBottom ? BorderManager.SendBottom : BorderManager.SendTop);
                    return;
                }

                if (sendBottom)
                {
                    Interlocked.Exchange(ref _pendingSendBottom, 1);
                }
                else
                {
                    Interlocked.Exchange(ref _pendingSendTop, 1);
                }

                var remainingTicks = Math.Max(1, ForegroundDispatchDebounceTicks - elapsedTicks);
                StartOrResetDebounceTimer(TimeSpan.FromTicks(remainingTicks));
                return;
            }
        }

        private static void StartOrResetDebounceTimer(TimeSpan dueIn)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            _debounceTimer ??= new DispatcherTimer(DispatcherPriority.Background, dispatcher);
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

            var dispatchBottom = Interlocked.Exchange(ref _pendingSendBottom, 0) == 1;
            var dispatchTop = Interlocked.Exchange(ref _pendingSendTop, 0) == 1;
            Interlocked.Exchange(ref _lastDispatchTicks, DateTime.UtcNow.Ticks);

            if (!dispatchBottom && !dispatchTop)
            {
                return;
            }

            if (dispatchBottom)
            {
                BeginOnUi(BorderManager.SendBottom);
            }
            if (dispatchTop)
            {
                BeginOnUi(BorderManager.SendTop);
            }
        }

        private static Rect GetWindowBounds(IntPtr current)
        {
            _ = NativeMethods.GetWindowRect(current, out var appBounds);
            return (Rect)(appBounds);
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

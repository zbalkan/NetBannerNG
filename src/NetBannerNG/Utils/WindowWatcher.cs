using NetBannerNG.Common.AppBar;
using NetBannerNG.Common.Native;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace NetBannerNG.Utils
{
    internal static class WindowWatcher
    {
        private const int EventObjectCreate = 0x8000;
        private const int EventObjectLocationChange = 0x800B;

        private static readonly NativeMethods.WinEventHook ForegroundWindowHook = HookCallback;
        private static IntPtr DesktopHandle => NativeMethods.GetDesktopWindow(); //Window handle for the desktop
        private static IntPtr ShellHandle => NativeMethods.GetShellWindow(); //Window handle for the shell
        private static IntPtr TaskbarHandle => NativeMethods.FindWindow("Shell_TrayWnd", null!);
        private static IntPtr _previousForegroundWindowHandle;
        private static IntPtr _hookId;

        internal static void Watch() => _hookId = SetHook(ForegroundWindowHook);

        internal static void Unwatch()
        {
            if (_hookId == default)
            {
                return;
            }

            _ = NativeMethods.UnhookWinEvent(_hookId);
        }

        private static IntPtr SetHook(NativeMethods.WinEventHook hookProc) => NativeMethods.SetWinEventHook(EventObjectCreate,
                EventObjectLocationChange, IntPtr.Zero, hookProc, 0, 0,
                (int)NativeMethods.SetWinEventHookFlags.SkipOwnProcess);

        private static void HookCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild,
            uint dwEventThread, uint dwmsEventTime)
        {
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

            var windowTitle = GetWindowTitle(foregroundWindowHandle);
            var windowBounds = GetWindowBounds(foregroundWindowHandle);

            var isFullScreen = IsFullScreen(foregroundWindowHandle);
            var monitorBounds = Common.Native.Monitor.GetMonitorBounds(foregroundWindowHandle);
            Debug.WriteLine(
                $"Window title: {windowTitle} | Full screen: {isFullScreen} | Window Bounds: {(Rect)windowBounds} | Monitor bounds: {monitorBounds}");

            if (isFullScreen)
            {
                Dispatch(BorderManager.SendBottom);
                //var newBounds = GetModifiedFullscreenBound(foregroundWindowHandle);
                //ResizeForegroundWindow(foregroundWindowHandle, newBounds);
                //BorderManager.InitiateAllBorders(true, true);
            }
            else
            {
                Dispatch(BorderManager.SendTop);
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

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = NativeMethods.GetWindowTextLength(hWnd) + 1;
            var title = new StringBuilder(length);
            _ = NativeMethods.GetWindowText(hWnd, title, length);
            return title.ToString();
        }

        private static bool IsFullScreen(IntPtr current)
        {
            // Determine if the window is fullscreen
            var windowBounds = GetWindowBounds(current);
            var screenBounds = Common.Native.Monitor.GetMonitorBounds(current);
            var union = Rect.Union(screenBounds, windowBounds);
            return windowBounds.Equals(union);
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


        private static void Dispatch(Action action) => _ = Application.Current.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);

        private static Rect GetWindowBounds(IntPtr current)
        {
            _ = NativeMethods.GetWindowRect(current, out var appBounds);
            return appBounds.ToRect();
        }
    }
}

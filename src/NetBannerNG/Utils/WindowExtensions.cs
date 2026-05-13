using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using NetBannerNG.Borders;
using NetBannerNG.Common.AppBar;
using static NetBannerNG.Common.Native.NativeMethods;

namespace NetBannerNG.Utils
{
    internal static class WindowExtensions
    {
        internal static void DockTop(this BorderBase border, FrameworkElement? childElement = null, bool topMost = true) => Dock(border, DockEdge.Top, childElement, topMost);

        internal static void DockBottom(this BorderBase border, FrameworkElement? childElement = null, bool topMost = true) => Dock(border, DockEdge.Bottom, childElement, topMost);

        internal static void DockLeft(this BorderBase border, FrameworkElement? childElement = null, bool topMost = true) => Dock(border, DockEdge.Left, childElement, topMost);

        internal static void DockRight(this BorderBase border, FrameworkElement? childElement = null, bool topMost = true) => Dock(border, DockEdge.Right, childElement, topMost);

        internal static void Undock(this BorderBase border)
        {
            AppBarFunctions.SetAppBar(border, DockEdge.None, border.AppBarMessageKey);
            border.IsDocked = false;
        }

        private static void Dock(BorderBase border, DockEdge edge, FrameworkElement? childElement, bool topMost)
        {
            AppBarFunctions.SetAppBar(border, edge, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static Common.Native.Monitor GetMonitor(this Window window)
        {
            var windowHandle = window.GetHandle();
            var monitorHandle = Common.Native.Monitor.GetMonitorHandleFromWindow(windowHandle).Handle;
            var monitor = Common.Native.Monitor.AllMonitors.First(m => m.Handle == monitorHandle);
            return monitor;
        }

        internal static void HideFromTaskBar(this Window window)
        {
            const int gwlHwndParent = -8;
            const int gwlExstyle = -20;
            const int wsExToolwindow = 0x80;
            const int wsExAppwindow = 0x40000;

            var windowHandle = window.GetHandle();

            if (window.Owner != null)
            {
                SetLastError(0);
                var result = SetWindowLongPtr(windowHandle, gwlHwndParent, window.Owner.GetHandle());
                if (result == IntPtr.Zero)
                {
                    var lastError = Marshal.GetLastWin32Error();
                    if (lastError != 0)
                    {
                        throw new InvalidOperationException($"Failed to set window parent. Error code: {lastError}");
                    }
                }
            }

            var currentStyle = GetWindowLongPtr(windowHandle, gwlExstyle).ToInt64();
            var newStyle = (currentStyle | wsExToolwindow) & ~wsExAppwindow;
            SetLastError(0);
            var result2 = SetWindowLongPtr(windowHandle, gwlExstyle, new IntPtr(newStyle));
            if (result2 == IntPtr.Zero)
            {
                var lastError = Marshal.GetLastWin32Error();
                if (lastError != 0)
                {
                    throw new InvalidOperationException($"Failed to set extended window style. Error code: {lastError}");
                }
            }
        }
    }
}
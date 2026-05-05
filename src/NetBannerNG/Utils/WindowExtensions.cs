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
        internal static void DockTop(this Window window, FrameworkElement? childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Top, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void DockBottom(this Window window, FrameworkElement? childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Bottom, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void DockLeft(this Window window, FrameworkElement? childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Left, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void DockRight(this Window window, FrameworkElement? childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Right, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void Undock(this Window window)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.None, border.AppBarMessageKey);
            border.IsDocked = false;
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
                var result = SetWindowLong(windowHandle, gwlHwndParent, (int)window.Owner.GetHandle());
                if (result == 0)
                {
                    throw new InvalidOperationException($"Failed to set window parent. Error code: {Marshal.GetLastWin32Error()}");
                }
            }

            var currentStyle = GetWindowLong(windowHandle, gwlExstyle);
            var newStyle = (currentStyle | wsExToolwindow) & ~wsExAppwindow;
            var result2 = SetWindowLong(windowHandle, gwlExstyle, newStyle);
            if (result2 == 0)
            {
                throw new InvalidOperationException($"Failed to set extended window style. Error code: {Marshal.GetLastWin32Error()}");
            }
        }
    }
}
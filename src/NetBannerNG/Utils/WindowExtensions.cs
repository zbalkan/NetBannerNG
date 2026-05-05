using System.Linq;
using System.Windows;
using NetBannerNG.Borders;
using NetBannerNG.Common.AppBar;
using static NetBannerNG.Common.Native.NativeMethods;

namespace NetBannerNG.Utils
{
    internal static class WindowExtensions
    {
        internal static void DockTop(this Window window, FrameworkElement childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Top, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void DockBottom(this Window window, FrameworkElement childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Bottom, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void DockLeft(this Window window, FrameworkElement childElement = null, bool topMost = true)
        {
            var border = (BorderBase)window;
            AppBarFunctions.SetAppBar(window, DockEdge.Left, border.AppBarMessageKey, childElement, topMost);
            border.IsDocked = true;
        }

        internal static void DockRight(this Window window, FrameworkElement childElement = null, bool topMost = true)
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

        internal static void HideFromTaskManager(this Window window)
        {
            const int gwlHwndParent = -8;
            const int gwlExstyle = -20;
            const int wsExToolwindow = 0x80;
            const int wsExAppwindow = 0x40000;

            if (window.Owner != null)
            {
                _ = SetWindowLong(window.GetHandle(), gwlHwndParent, (int)window.Owner.GetHandle());
            }

            _ = SetWindowLong(window.GetHandle(), gwlExstyle,
                (GetWindowLong(window.GetHandle(), gwlExstyle) | wsExToolwindow) & ~wsExAppwindow);
        }
    }
}
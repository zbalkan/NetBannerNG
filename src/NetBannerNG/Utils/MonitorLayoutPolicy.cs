using System;
using NetBannerNG.Borders;
using NetBannerNG.Common.AppBar;
using static NetBannerNG.Common.Native.NativeTypes;
using static NetBannerNG.Common.Native.User32;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    internal static class MonitorLayoutPolicy
    {
        internal static void ApplyMonitorBounds(BorderBase window, Monitor monitor)
        {
            // Anchor the window onto the target monitor in physical screen pixels. The bar's
            // final size and edge alignment are then computed by AbSetPos (via Render(true))
            // using Monitor.GetMonitorWorkArea(hWnd) on the freshly anchored HWND.
            //
            // Using SetWindowPos with raw pixels avoids the previous bug where Monitor.Bounds
            // (in physical pixels from GetMonitorInfo) was assigned to Window.Top/Left/Width
            // and reinterpreted as DIPs at the window's current per-monitor matrix, which on
            // non-100% monitors briefly oversized the window by the DPI ratio.
            var hwnd = window.GetHandle();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            _ = SetWindowPos(
                hwnd,
                IntPtr.Zero,
                (int)monitor.Bounds.Left,
                (int)monitor.Bounds.Top,
                (int)monitor.Bounds.Width,
                (int)monitor.Bounds.Height,
                SetWindowPosFlags.IgnoreZOrder | SetWindowPosFlags.DoNotActivate);
        }

        internal static double GetVerticalTop(Monitor monitor) => monitor.Bounds.Top + Settings.Instance.BannerSize;

        internal static double GetVerticalHeight(Monitor monitor) =>
            Math.Max(1, monitor.Bounds.Height - Settings.Instance.BannerSize - Settings.Instance.BorderSize);
    }
}

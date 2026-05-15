using System;
using NetBannerNG.Borders;
using Monitor = NetBannerNG.Common.Monitor;

namespace NetBannerNG.Utils
{
    internal static class MonitorLayoutPolicy
    {
        internal static void ApplyMonitorBounds(BorderBase window, Monitor monitor)
        {
            window.Top = monitor.Bounds.Top;
            window.Left = monitor.Bounds.Left;

            switch (window)
            {
                case Banner or BottomBanner or BottomBar:
                    window.Width = monitor.Bounds.Width;
                    break;

                case LeftBar:
                    window.Top = GetVerticalTop(monitor);
                    window.Height = GetVerticalHeight(monitor);
                    window.Left = monitor.Bounds.Left;
                    break;

                case RightBar:
                    window.Top = GetVerticalTop(monitor);
                    window.Height = GetVerticalHeight(monitor);
                    window.Left = monitor.Bounds.Right - Settings.Instance.BorderSize;
                    break;
            }
        }

        internal static double GetVerticalTop(Monitor monitor) => monitor.Bounds.Top + Settings.Instance.BannerSize;

        internal static double GetVerticalHeight(Monitor monitor) =>
            Math.Max(1, monitor.Bounds.Height - Settings.Instance.BannerSize - Settings.Instance.BorderSize);
    }
}
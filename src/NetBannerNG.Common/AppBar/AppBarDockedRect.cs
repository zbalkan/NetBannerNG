using System;

namespace NetBannerNG.Common.AppBar
{
    /// <summary>
    /// Pure geometry for the appbar docked rectangle. Given a monitor work area in physical
    /// screen pixels and the bar's thickness (perpendicular to the docking edge) in physical
    /// pixels, returns the rectangle the bar should occupy on screen, also in physical pixels.
    /// <para>
    /// Everything here is in PHYSICAL PIXELS. DIP-to-pixel conversion is the caller's
    /// responsibility, deliberately, so that the geometry under test is decoupled from WPF's
    /// per-monitor DPI matrix. The historical bug ("the bar ends up 3x too wide on a 300%
    /// monitor") was caused by mixing units in the caller; pinning units down here is what
    /// makes the regression catchable.
    /// </para>
    /// </summary>
    public static class AppBarDockedRect
    {
        /// <param name="edge">Edge the bar is docked against. <see cref="DockEdge.None"/> returns the work area unchanged.</param>
        /// <param name="workArea">Monitor work area in physical screen pixels (as returned by GetMonitorInfo).</param>
        /// <param name="thicknessInPixels">Bar size along the axis perpendicular to <paramref name="edge"/>, in physical pixels. Negative values are clamped to zero.</param>
        public static MonitorRect Calculate(DockEdge edge, MonitorRect workArea, int thicknessInPixels)
        {
            var t = Math.Max(0, thicknessInPixels);

            return edge switch
            {
                DockEdge.Top => new MonitorRect
                {
                    Left = workArea.Left,
                    Right = workArea.Right,
                    Top = workArea.Top,
                    Bottom = workArea.Top + t,
                },
                DockEdge.Bottom => new MonitorRect
                {
                    Left = workArea.Left,
                    Right = workArea.Right,
                    Bottom = workArea.Bottom,
                    Top = workArea.Bottom - t,
                },
                DockEdge.Left => new MonitorRect
                {
                    Top = workArea.Top,
                    Bottom = workArea.Bottom,
                    Left = workArea.Left,
                    Right = workArea.Left + t,
                },
                DockEdge.Right => new MonitorRect
                {
                    Top = workArea.Top,
                    Bottom = workArea.Bottom,
                    Right = workArea.Right,
                    Left = workArea.Right - t,
                },
                _ => workArea,
            };
        }
    }
}
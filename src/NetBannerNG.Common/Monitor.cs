using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using NetBannerNG.Common.Native;

namespace NetBannerNG.Common
{
    public sealed class Monitor : IEquatable<Monitor>
    {
        private static readonly HandleRef HandleRef = new(null, IntPtr.Zero);

        private Monitor(IntPtr monitor, IntPtr? hdc)
        {
            if (hdc is null)
            {
                throw new ArgumentNullException(nameof(hdc));
            }

            var info = new MonitorInfoEx();
            _ = User32.GetMonitorInfo(new HandleRef(null, monitor), info);
            Bounds = new Rect(
                info.rcMonitor.Left, info.rcMonitor.Top,
                info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top);
            WorkingArea = new Rect(
                info.rcWork.Left, info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left,
                info.rcWork.Bottom - info.rcWork.Top);
            IsPrimary = (info.dwFlags & (int)NativeTypes.MonitorDefaultTo.Primary) != 0;
            Name = new string(info.szDevice).TrimEnd((char)0);
            Handle = monitor;

            Debug.WriteLine(ToString());
        }

        public static IEnumerable<Monitor> AllMonitors {
            get {
                var closure = new MonitorEnumCallback();
                var proc = new MonitorEnumProc(closure.Callback);
                _ = User32.EnumDisplayMonitors(HandleRef, IntPtr.Zero, proc, IntPtr.Zero);
                var monitors = closure.Monitors.Cast<Monitor>().ToList();
                if (monitors.Count > 0)
                {
                    return monitors;
                }

                return new[] { CreateFallbackPrimaryMonitor() };
            }
        }

        public Rect Bounds { get; }
        public bool IsPrimary { get; }
        public string Name { get; }
        public Rect WorkingArea { get; }
        public IntPtr Handle { get; }

        public static HandleRef GetMonitorHandleFromWindow(IntPtr hWnd)
        {
            var ptrMonitor = User32.MonitorFromWindow(hWnd, NativeTypes.MonitorDefaultTo.Nearest);
            return new HandleRef(null, ptrMonitor);
        }

        public static MonitorRect GetMonitorWorkArea(IntPtr hWindow)
        {
            var monitorInfoEx = new MonitorInfoEx();
            var hMonitor = GetMonitorHandleFromWindow(hWindow);
            _ = User32.GetMonitorInfo(hMonitor, monitorInfoEx);
            return monitorInfoEx.rcWork;
        }

        public static MonitorRect GetMonitorBounds(IntPtr hWindow)
        {
            var monitorInfoEx = new MonitorInfoEx();
            var hMonitor = GetMonitorHandleFromWindow(hWindow);
            _ = User32.GetMonitorInfo(hMonitor, monitorInfoEx);
            return monitorInfoEx.rcMonitor;
        }

        public override string ToString() => $"Name: {Name} | IsPrimary: {IsPrimary} | Bounds: {Bounds} | WorkingArea: {WorkingArea}";

        public override bool Equals(object? obj) => obj is Monitor monitor && EqualityComparer<Rect>.Default.Equals(Bounds, monitor.Bounds) && IsPrimary == monitor.IsPrimary && Name == monitor.Name && EqualityComparer<Rect>.Default.Equals(WorkingArea, monitor.WorkingArea) && Handle.Equals(monitor.Handle);

        public bool Equals(Monitor? other) =>
            other is not null &&
            EqualityComparer<Rect>.Default.Equals(Bounds, other.Bounds) &&
            IsPrimary == other.IsPrimary &&
            Name == other.Name &&
            EqualityComparer<Rect>.Default.Equals(WorkingArea, other.WorkingArea) &&
            Handle.Equals(other.Handle);

        public override int GetHashCode() => (Bounds, IsPrimary, Name, WorkingArea, Handle).GetHashCode();

        public delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam);

        private static Monitor CreateFallbackPrimaryMonitor()
        {
            var desktopWindow = User32.GetDesktopWindow();
            var desktopMonitorHandle = User32.MonitorFromWindow(desktopWindow, NativeTypes.MonitorDefaultTo.Primary);
            return new Monitor(desktopMonitorHandle, IntPtr.Zero);
        }

        private class MonitorEnumCallback
        {
            public List<Monitor> Monitors { get; } = new();

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "")]
            public bool Callback(IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam)
            {
                Monitors.Add(new Monitor(monitor, hdc));
                return true;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
        internal class MonitorInfoEx : IEquatable<MonitorInfoEx>
        {
            public int cbSize = Marshal.SizeOf<MonitorInfoEx>();

            public MonitorRect rcMonitor;
            public MonitorRect rcWork;
            public int dwFlags;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] szDevice = new char[32];

            public override bool Equals(object? obj) => obj is MonitorInfoEx ex && cbSize == ex.cbSize && rcMonitor.Equals(ex.rcMonitor) && rcWork.Equals(ex.rcWork) && dwFlags == ex.dwFlags && EqualityComparer<char[]>.Default.Equals(szDevice, ex.szDevice);

            public bool Equals(MonitorInfoEx? other) =>
                other is not null &&
                cbSize == other.cbSize &&
                rcMonitor.Equals(other.rcMonitor) &&
                rcWork.Equals(other.rcWork) &&
                dwFlags == other.dwFlags &&
                ((szDevice == other.szDevice) || (szDevice != null && other.szDevice != null && szDevice.SequenceEqual(other.szDevice)));

            public override int GetHashCode() => (cbSize, rcMonitor, rcWork, dwFlags, szDevice).GetHashCode();

            public static bool operator ==(MonitorInfoEx? left, MonitorInfoEx? right) => EqualityComparer<MonitorInfoEx?>.Default.Equals(left, right);

            public static bool operator !=(MonitorInfoEx? left, MonitorInfoEx? right) => !(left == right);

            private string GetDebuggerDisplay() => ToString() ?? string.Empty;
        }

        public static bool operator ==(Monitor? left, Monitor? right) => EqualityComparer<Monitor?>.Default.Equals(left, right);

        public static bool operator !=(Monitor? left, Monitor? right) => !(left == right);
    }
}

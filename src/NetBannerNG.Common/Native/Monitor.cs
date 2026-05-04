using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace NetBannerNG.Common.Native
{
    public sealed class Monitor : IEquatable<Monitor>
    {
        private static readonly HandleRef HandleRef = new(null, System.IntPtr.Zero);

        private Monitor(System.IntPtr monitor, System.IntPtr? hdc)
        {
            var info = new MonitorInfoEx();
            _ = NativeMethods.GetMonitorInfo(new HandleRef(null, monitor), info);
            Bounds = new Rect(
                info.rcMonitor.Left, info.rcMonitor.Top,
                info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top);
            WorkingArea = new Rect(
                info.rcWork.Left, info.rcWork.Top,
                info.rcWork.Right - info.rcWork.Left,
                info.rcWork.Bottom - info.rcWork.Top);
            IsPrimary = (info.dwFlags & (int)NativeMethods.MonitorDefaultTo.Primary) != 0;
            Name = new string(info.szDevice).TrimEnd((char)0);
            Handle = monitor;

            Debug.WriteLine(ToString());
        }

        public static IEnumerable<Monitor> AllMonitors
        {
            get
            {
                var closure = new MonitorEnumCallback();
                var proc = new MonitorEnumProc(closure.Callback);
                _ = NativeMethods.EnumDisplayMonitors(HandleRef, System.IntPtr.Zero, proc, System.IntPtr.Zero);
                return closure.Monitors.Cast<Monitor>();
            }
        }

        public Rect Bounds { get; }
        public bool IsPrimary { get; }
        public string Name { get; }
        public Rect WorkingArea { get; }
        public System.IntPtr Handle { get; }

        public static HandleRef GetMonitorHandleFromWindow(System.IntPtr hWnd)
        {
            var ptrMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MonitorDefaultTo.Nearest);
            return new HandleRef(null, ptrMonitor);
        }

        public static MonitorRect GetMonitorWorkArea(System.IntPtr hWindow)
        {
            var monitorInfoEx = new MonitorInfoEx();
            var hMonitor = GetMonitorHandleFromWindow(hWindow);
            _ = NativeMethods.GetMonitorInfo(hMonitor, monitorInfoEx);
            return monitorInfoEx.rcWork;
        }

        public static MonitorRect GetMonitorBounds(System.IntPtr hWindow)
        {
            var monitorInfoEx = new MonitorInfoEx();
            var hMonitor = GetMonitorHandleFromWindow(hWindow);
            _ = NativeMethods.GetMonitorInfo(hMonitor, monitorInfoEx);
            return monitorInfoEx.rcMonitor;
        }

        public override string ToString()
        {
            return $"Name: {Name} | IsPrimary: {IsPrimary} | Bounds: {Bounds} | WorkingArea: {WorkingArea}";
        }

        public override bool Equals(object? obj) => obj is Monitor monitor && EqualityComparer<Rect>.Default.Equals(Bounds, monitor.Bounds) && IsPrimary == monitor.IsPrimary && Name == monitor.Name && EqualityComparer<Rect>.Default.Equals(WorkingArea, monitor.WorkingArea) && Handle.Equals(monitor.Handle);

        public bool Equals(Monitor? other) => Equals(other);

        public override int GetHashCode() => (Bounds, IsPrimary, Name, WorkingArea, Handle).GetHashCode();

        public delegate bool MonitorEnumProc(System.IntPtr monitor, System.IntPtr hdc, System.IntPtr lprcMonitor, System.IntPtr lParam);

        private class MonitorEnumCallback
        {
            public ArrayList Monitors { get; }

            public MonitorEnumCallback() => Monitors = new ArrayList();

            public bool Callback(System.IntPtr monitor, System.IntPtr hdc, System.IntPtr lprcMonitor, System.IntPtr lParam)
            {
                _ = Monitors.Add(new Monitor(monitor, hdc));
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

            public bool Equals(MonitorInfoEx? other) => Equals(other);

            public override int GetHashCode() => (cbSize, rcMonitor, rcWork, dwFlags, szDevice).GetHashCode();

            public static bool operator ==(MonitorInfoEx? left, MonitorInfoEx? right) => EqualityComparer<MonitorInfoEx>.Default.Equals(left, right);

            public static bool operator !=(MonitorInfoEx left, MonitorInfoEx right) => !(left == right);

            private string GetDebuggerDisplay() => ToString() ?? string.Empty;
        }

        public static bool operator ==(Monitor? left, Monitor? right) => EqualityComparer<Monitor>.Default.Equals(left, right);

        public static bool operator !=(Monitor left, Monitor right) => !(left == right);
    }
}
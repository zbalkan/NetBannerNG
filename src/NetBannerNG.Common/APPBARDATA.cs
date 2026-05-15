using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetBannerNG.Common
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal struct APPBARDATA : IEquatable<APPBARDATA>
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public MonitorRect rc;
        public IntPtr lParam;

        public readonly override bool Equals(object? obj) => obj is APPBARDATA aPPBARDATA && Equals(aPPBARDATA);

        public readonly bool Equals(APPBARDATA other) => cbSize == other.cbSize && hWnd.Equals(other.hWnd) && uCallbackMessage == other.uCallbackMessage && uEdge == other.uEdge && rc.Equals(other.rc) && lParam.Equals(other.lParam);

        public readonly override int GetHashCode() => (cbSize, hWnd, uCallbackMessage, uEdge, rc, lParam).GetHashCode();

        public static bool operator ==(APPBARDATA left, APPBARDATA right) => left.Equals(right);

        public static bool operator !=(APPBARDATA left, APPBARDATA right) => !(left == right);

        private readonly string GetDebuggerDisplay() => ToString() ?? string.Empty;
    }
}

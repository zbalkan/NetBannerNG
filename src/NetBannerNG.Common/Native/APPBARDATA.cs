using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal struct APPBARDATA : IEquatable<APPBARDATA>
    {
        public int cbSize;
        public System.IntPtr hWnd;
        public int uCallbackMessage;
        public int uEdge;
        public MonitorRect rc;
        public System.IntPtr lParam;

        public override readonly bool Equals(object? obj) => obj is APPBARDATA aPPBARDATA && Equals(aPPBARDATA);

        public readonly bool Equals(APPBARDATA other) => cbSize == other.cbSize && hWnd.Equals(other.hWnd) && uCallbackMessage == other.uCallbackMessage && uEdge == other.uEdge && rc.Equals(other.rc) && lParam.Equals(other.lParam);

        public override readonly int GetHashCode() => (cbSize, hWnd, uCallbackMessage, uEdge, rc, lParam).GetHashCode();

        public static bool operator ==(APPBARDATA left, APPBARDATA right) => left.Equals(right);

        public static bool operator !=(APPBARDATA left, APPBARDATA right) => !(left == right);

        private readonly string GetDebuggerDisplay() => ToString() ?? string.Empty;
    }
}
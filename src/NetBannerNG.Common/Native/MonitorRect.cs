using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace NetBannerNG.Common.Native
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct MonitorRect : IEquatable<MonitorRect>
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public override readonly string ToString() => $"{Left},{Top},{Right},{Bottom}";

        public readonly bool Equals(MonitorRect other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override readonly bool Equals(object? obj) => obj is MonitorRect other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

        public static bool operator ==(MonitorRect left, MonitorRect right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MonitorRect left, MonitorRect right)
        {
            return !left.Equals(right);
        }

        public static implicit operator Rect(MonitorRect mr)
        {
            return new Rect(new Point(mr.Left, mr.Top), new Point(mr.Right, mr.Bottom));
        }

        public readonly Rect ToRect() => (Rect)this;

        private readonly string GetDebuggerDisplay() => ToString();
    }
}
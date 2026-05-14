using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace NetBannerNG.Common
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public struct MonitorRect : IEquatable<MonitorRect>
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public int X {
            readonly get => Left;
            set {
                var width = Width;
                Left = value;
                Right = value + width;
            }
        }

        public int Y {
            readonly get => Top;
            set {
                var height = Height;
                Top = value;
                Bottom = value + height;
            }
        }

        public int Width {
            readonly get => Right - Left;
            set {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Width cannot be negative.");
                }

                Right = Left + value;
            }
        }

        public int Height {
            readonly get => Bottom - Top;
            set {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Height cannot be negative.");
                }

                Bottom = Top + value;
            }
        }

        public readonly bool IsEmpty => Width <= 0 || Height <= 0;

        public readonly Rect ToWpfRect() => new Rect(X, Y, Width, Height);

        public static implicit operator Rect(MonitorRect mr)
        {
            return mr.ToWpfRect();
        }

        public override readonly string ToString() => $"{Left},{Top},{Right},{Bottom}";

        public readonly bool Equals(MonitorRect other) => Left == other.Left
                && Top == other.Top
                && Right == other.Right
                && Bottom == other.Bottom;

        public override readonly bool Equals(object obj) => obj is MonitorRect other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

        public static bool operator ==(MonitorRect left, MonitorRect right) => left.Equals(right);

        public static bool operator !=(MonitorRect left, MonitorRect right) => !left.Equals(right);

        private readonly string GetDebuggerDisplay() => $"{Left},{Top},{Right},{Bottom} ({Width}x{Height})";
    }
}
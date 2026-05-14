using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetBannerNG.Common
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal struct SecurityAttributes : IEquatable<SecurityAttributes>
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;

        public override readonly bool Equals(object? obj) => obj is SecurityAttributes attributes && Equals(attributes);

        public readonly bool Equals(SecurityAttributes other) => nLength == other.nLength && lpSecurityDescriptor.Equals(other.lpSecurityDescriptor) && bInheritHandle == other.bInheritHandle;

        public override readonly int GetHashCode() => (nLength, lpSecurityDescriptor, bInheritHandle).GetHashCode();

        public static bool operator ==(SecurityAttributes left, SecurityAttributes right) => left.Equals(right);

        public static bool operator !=(SecurityAttributes left, SecurityAttributes right) => !(left == right);

        private readonly string GetDebuggerDisplay() => ToString() ?? string.Empty;
    }
}
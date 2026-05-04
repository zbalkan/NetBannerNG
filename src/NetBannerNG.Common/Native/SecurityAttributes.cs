using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal struct SecurityAttributes : IEquatable<SecurityAttributes>
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;

        public readonly override bool Equals(object? obj) => obj is SecurityAttributes attributes && Equals(attributes);

        public readonly bool Equals(SecurityAttributes other) => nLength == other.nLength && lpSecurityDescriptor.Equals(other.lpSecurityDescriptor) && bInheritHandle == other.bInheritHandle;

        public readonly override int GetHashCode() => (nLength, lpSecurityDescriptor, bInheritHandle).GetHashCode();

        public static bool operator ==(SecurityAttributes left, SecurityAttributes right) => left.Equals(right);

        public static bool operator !=(SecurityAttributes left, SecurityAttributes right) => !(left == right);

        private readonly string GetDebuggerDisplay() => ToString() ?? string.Empty;
    }
}
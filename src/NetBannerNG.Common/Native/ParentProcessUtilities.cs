using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    /// <summary>
    /// A utility class to determine a process parent.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ParentProcessUtilities : IEquatable<ParentProcessUtilities>
    {
        /// <summary>
        /// These members must match PROCESS_BASIC_INFORMATION
        /// </summary>
        public nint Reserved1;

        public nint PebBaseAddress;
        public nint Reserved2_0;
        public nint Reserved2_1;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;

        public override readonly bool Equals(object? obj) => obj is ParentProcessUtilities utilities && Equals(utilities);

        public readonly bool Equals(ParentProcessUtilities other) => Reserved1.Equals(other.Reserved1) && PebBaseAddress.Equals(other.PebBaseAddress) && Reserved2_0.Equals(other.Reserved2_0) && Reserved2_1.Equals(other.Reserved2_1) && UniqueProcessId.Equals(other.UniqueProcessId) && InheritedFromUniqueProcessId.Equals(other.InheritedFromUniqueProcessId);

        public override readonly int GetHashCode() => HashCode.Combine(Reserved1, PebBaseAddress, Reserved2_0, Reserved2_1, UniqueProcessId, InheritedFromUniqueProcessId);

        public static bool operator ==(ParentProcessUtilities left, ParentProcessUtilities right) => left.Equals(right);

        public static bool operator !=(ParentProcessUtilities left, ParentProcessUtilities right) => !(left == right);
    }
}
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
        public IntPtr Reserved1;

        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;

        public readonly override bool Equals(object? obj) => obj is ParentProcessUtilities utilities && Equals(utilities);

        public readonly bool Equals(ParentProcessUtilities other) => Reserved1.Equals(other.Reserved1) && PebBaseAddress.Equals(other.PebBaseAddress) && Reserved2_0.Equals(other.Reserved2_0) && Reserved2_1.Equals(other.Reserved2_1) && UniqueProcessId.Equals(other.UniqueProcessId) && InheritedFromUniqueProcessId.Equals(other.InheritedFromUniqueProcessId);

        public readonly override int GetHashCode() => (Reserved1, PebBaseAddress, Reserved2_0, Reserved2_1, UniqueProcessId, InheritedFromUniqueProcessId).GetHashCode();

        public static bool operator ==(ParentProcessUtilities left, ParentProcessUtilities right) => left.Equals(right);

        public static bool operator !=(ParentProcessUtilities left, ParentProcessUtilities right) => !(left == right);
    }
}
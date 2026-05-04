using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    internal struct ProcessInformation : IEquatable<ProcessInformation>
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;

        public override readonly bool Equals(object? obj) => obj is ProcessInformation information && Equals(information);

        public readonly bool Equals(ProcessInformation other) => hProcess.Equals(other.hProcess) && hThread.Equals(other.hThread) && dwProcessId == other.dwProcessId && dwThreadId == other.dwThreadId;

        public override readonly int GetHashCode() => HashCode.Combine(hProcess, hThread, dwProcessId, dwThreadId);

        public static bool operator ==(ProcessInformation left, ProcessInformation right) => left.Equals(right);

        public static bool operator !=(ProcessInformation left, ProcessInformation right) => !(left == right);

        private readonly string GetDebuggerDisplay() => ToString() ?? string.Empty;
    }
}
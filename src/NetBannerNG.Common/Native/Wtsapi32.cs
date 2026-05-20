using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    public static class Wtsapi32
    {
        public enum WTSINFOCLASS
        {
            None = 0,
            WTSUserName = 5,
            WTSDomainName = 7
        }

        public enum WTSCONNECTSTATECLASS
        {
            WTSActive = 0,
            WTSConnected = 1,
            WTSConnectQuery = 2,
            WTSShadow = 3,
            WTSDisconnected = 4,
            WTSIdle = 5,
            WTSListen = 6,
            WTSReset = 7,
            WTSDown = 8,
            WTSInit = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WTSSESSIONINFO : IEquatable<WTSSESSIONINFO>
        {
            public uint SessionId;
            public IntPtr pWinStationName;
            public WTSCONNECTSTATECLASS State;

            public readonly override bool Equals(object? obj) => obj is WTSSESSIONINFO wTSSESSIONINFO && Equals(wTSSESSIONINFO);
            public readonly bool Equals(WTSSESSIONINFO other) => SessionId == other.SessionId && EqualityComparer<IntPtr>.Default.Equals(pWinStationName, other.pWinStationName) && State == other.State;

            public readonly override int GetHashCode() => HashCode.Combine(SessionId, pWinStationName, State);

            public static bool operator ==(WTSSESSIONINFO left, WTSSESSIONINFO right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(WTSSESSIONINFO left, WTSSESSIONINFO right)
            {
                return !(left == right);
            }
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);



        [DllImport("wtsapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            int reserved,
            int version,
            out IntPtr ppSessionInfo,
            out int pCount);

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern bool WTSQuerySessionInformation(
            IntPtr hServer,
            uint sessionId,
            WTSINFOCLASS wtsInfoClass,
            out IntPtr ppBuffer,
            out uint pBytesReturned);

        [DllImport("wtsapi32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern void WTSFreeMemory(IntPtr pMemory);
    }
}

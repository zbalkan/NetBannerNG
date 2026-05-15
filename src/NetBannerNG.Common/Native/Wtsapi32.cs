using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    public static class Wtsapi32
    {
        [DllImport("wtsapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);
    }
}
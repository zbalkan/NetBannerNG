using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace NetBannerNG.Common.Native
{
    public static class Shell32
    {
        [DllImport("SHELL32", CallingConvention = CallingConvention.StdCall)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);
    }
}

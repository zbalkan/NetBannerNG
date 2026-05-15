using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    public static class UserEnv
    {
        [DllImport("userenv.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool CreateEnvironmentBlock(out IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);
    }
}
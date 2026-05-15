using System.Runtime.InteropServices;

namespace NetBannerNG.Common.Native
{
    public static class NtDll
    {
        [DllImport("ntdll.dll", CharSet = CharSet.Auto)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);
    }
}
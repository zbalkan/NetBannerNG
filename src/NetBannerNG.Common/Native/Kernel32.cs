using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace NetBannerNG.Common.Native
{
    public static class Kernel32
    {
        // Sufficient to call QueryFullProcessImageNameW against a higher-integrity
        // process (e.g. a LocalSystem service) from a normal user token.
        [CLSCompliant(false)]
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
#pragma warning restore CA1707 // Identifiers should not contain underscores

        [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        public static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes

        [method: CLSCompliant(false)]
        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern void SetLastError(uint dwErrCode);

        /// <summary>
        /// The WTSGetActiveConsoleSessionId function retrieves the Remote Desktop Services session that
        /// is currently attached to the physical console. The physical console is the monitor, keyboard, and mouse.
        /// Note that it is not necessary that Remote Desktop Services be running for this function to succeed.
        /// </summary>
        /// <returns>The session identifier of the session that is attached to the physical console. If there is no
        /// session attached to the physical console, (for example, if the physical console session is in the process
        /// of being attached or detached), this function returns 0xFFFFFFFF. Also, it might return 0 if the Remote
        /// Desktop Service is started but only the SYSTEM is using.
        /// <see href="https://fleexlab.blogspot.com/2015/04/remote-desktop-surprise.html"/></returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);
    }
}
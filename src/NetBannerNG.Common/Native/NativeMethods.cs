using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;

[assembly: CLSCompliant(true)]

namespace NetBannerNG.Common.Native
{
    public static class NativeMethods
    {
        #region Security

        [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurity]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool CloseHandle(IntPtr handle);

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

        [DllImport("wtsapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            ref SecurityAttributes lpProcessAttributes,
            ref SecurityAttributes lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref StartupInfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool DuplicateToken(IntPtr existingTokenHandle, int securityImpersonationLevel, ref IntPtr duplicateTokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool DuplicateToken(IntPtr existingTokenHandle, SecurityImpersonationLevel securityImpersonationLevel, ref IntPtr duplicateTokenHandle);

        [DllImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurity]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int OpenProcessToken(IntPtr processHandle, // handle to process
            int desiredAccess, // desired access to process
            ref IntPtr tokenHandle // handle to open access token
        );

        [DllImport("advapi32", SetLastError = true), SuppressUnmanagedCodeSecurity]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int OpenProcessToken(IntPtr processHandle, // handle to process
            TokenAccessRights desiredAccess, // desired access to process
            ref IntPtr tokenHandle // handle to open access token
        );

        #endregion Security

        #region Shell

        #region Interop Functions

        [DllImport("SHELL32", CallingConvention = CallingConvention.StdCall)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int RegisterWindowMessage(string msg);

        [DllImport("dwmapi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int attrValue, int attrSize);

        [DllImport("User32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern IntPtr MonitorFromWindow(IntPtr hWnd, MonitorDefaultTo dwFlags);

        [DllImport("user32.dll", ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool EnumDisplayMonitors(HandleRef hdc, IntPtr rcClip, Monitor.MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool GetMonitorInfo(HandleRef hMonitor, [In, Out] Monitor.MonitorInfoEx info);

        [DllImport("user32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [CLSCompliant(false)]
        public static extern IntPtr SetWinEventHook(int eventMin, int eventMax, IntPtr hmodWinEventProc, WinEventHook lpfnWinEventProc, int idProcess, int idThread, int dwflags);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetWindowRect(IntPtr hWnd, out MonitorRect rc);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool GetKernelObjectSecurity(IntPtr handle, int securityInformation, [Out] byte[] pSecurityDescriptor, uint nLength, out uint lpnLengthNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern bool SetKernelObjectSecurity(IntPtr handle, int securityInformation, [In] byte[] pSecurityDescriptor);

        [DllImport("ntdll.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref ParentProcessUtilities processInformation, int processInformationLength, out int returnLength);

        #endregion Interop Functions

        #region WinEvent Hook

        [CLSCompliant(false)]
        public delegate void WinEventHook(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        #endregion WinEvent Hook

        #region Enums

        public enum MonitorDefaultTo
        {
            Null = 0,
            Primary = 1,
            Nearest = 2
        }

        public enum AbMsg
        {
            AbmNew = 0,
            AbmRemove = 1,
            AbmQuerypos = 2,
            AbmSetpos = 3,
            AbmGetstate = 4,
            AbmGettaskbarpos = 5,
            AbmActivate = 6,
            AbmGetautohidebar = 7,
            AbmSetautohidebar = 8,
            AbmWindowposchanged = 9,
            AbmSetstate = 10
        }

        public enum AbNotify
        {
            AbnStatechange = 0,
            AbnPoschanged = 1,
            AbnFullscreenapp = 2,
            AbnWindowarrange = 3
        }

        [Flags]
        internal enum DwmncRenderingPolicy
        {
            UseWindowStyle = 0,
            Disabled = 1,
            Enabled = 1 << 1,
            Last = 1 << 2
        }

        [Flags]
        internal enum DWMWINDOWATTRIBUTE
        {
            None = 0,
            DwmaNcrenderingEnabled = 1,
            DwmaNcrenderingPolicy = 1 << 1,
            DwmaTransitionsForcedisabled = 1 << 2,
            DwmaAllowNcpaint = 1 << 3,
            DwmaCpationButtonBounds = 1 << 4,
            DwmaNonclientRtlLayout = 1 << 5,
            DwmaForceIconicRepresentation = 1 << 6,
            DwmaFlip3DPolicy = 1 << 7,
            DwmaExtendedFrameBounds = 1 << 8,
            DwmaHasIconicBitmap = 1 << 9,
            DwmaDisallowPeek = 1 << 10,
            DwmaExcludedFromPeek = 1 << 11,
            DwmaLast = 1 << 12
        }

        [Flags]
        internal enum SetWindowPosFlags : uint
        {
            None = 0,
            IgnoreResize = 0x0001,
            IgnoreMove = 1 << 1,
            IgnoreZOrder = 1 << 2,
            DoNotRedraw = 1 << 3,
            DoNotActivate = 1 << 4,
            DrawFrame = 1 << 5,
            FrameChanged = 1 << 5,
            ShowWindow = 1 << 6,
            HideWindow = 1 << 7,
            DoNotCopyBits = 1 << 8,
            DoNotChangeOwnerZOrder = 1 << 9,
            DoNotReposition = 1 << 9,
            DoNotSendChangingEvent = 1 << 10,
            DeferErase = 1 << 13,
            SynchronousWindowPosition = 1 << 14,
        }

        [Flags]
        public enum SetWinEventHookFlags
        {
            OutOfContext = 0,
            SkipOwnThread = 1,
            SkipOwnProcess = 1 << 1,
            InContext = 1 << 2
        }

        #endregion Enums

        #endregion Shell
    }
}

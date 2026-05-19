namespace NetBannerNG.Common.Native
{
    public static class NativeTypes
    {
        [CLSCompliant(false)]
        public delegate void WinEventHook(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

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

        internal enum DWMWINDOWATTRIBUTE
        {
            None = 0,
            DwmaNcrenderingEnabled = 1,
            DwmaNcrenderingPolicy = 2,
            DwmaTransitionsForcedisabled = 3,
            DwmaAllowNcpaint = 4,
            DwmaCpationButtonBounds = 5,
            DwmaNonclientRtlLayout = 6,
            DwmaForceIconicRepresentation = 7,
            DwmaFlip3DPolicy = 8,
            DwmaExtendedFrameBounds = 9,
            DwmaHasIconicBitmap = 10,
            DwmaDisallowPeek = 11,
            DwmaExcludedFromPeek = 12,
            DwmaLast = 13
        }

        [Flags]
        [CLSCompliant(false)]
#pragma warning disable CA1028 // Enum Storage should be Int32
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
        public enum SetWindowPosFlags : uint
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
#pragma warning restore CA1028 // Enum Storage should be Int32
        {
            None = 0,
            IgnoreResize = 0x0001,
            IgnoreMove = 1 << 1,
            IgnoreZOrder = 1 << 2,
            DoNotRedraw = 1 << 3,
            DoNotActivate = 1 << 4,
            DrawFrame = 1 << 5,
#pragma warning disable CA1069 // Enums values should not be duplicated
            FrameChanged = 1 << 5,
#pragma warning restore CA1069 // Enums values should not be duplicated
            ShowWindow = 1 << 6,
            HideWindow = 1 << 7,
            DoNotCopyBits = 1 << 8,
            DoNotChangeOwnerZOrder = 1 << 9,
#pragma warning disable CA1069 // Enums values should not be duplicated
            DoNotReposition = 1 << 9,
#pragma warning restore CA1069 // Enums values should not be duplicated
            DoNotSendChangingEvent = 1 << 10,
            DeferErase = 1 << 13,
            SynchronousWindowPosition = 1 << 14,
        }

        [Flags]
        public enum SetWinEventHook
        {
#pragma warning disable CA1008 // Enums should have zero value
            OutOfContext = 0,
#pragma warning restore CA1008 // Enums should have zero value
            SkipOwnThread = 1,
            SkipOwnProcess = 1 << 1,
            InContext = 1 << 2
        }
    }
}
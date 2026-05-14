using System;

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
    }
}

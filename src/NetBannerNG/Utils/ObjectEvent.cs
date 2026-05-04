namespace NetBannerNG.Utils
{
    internal enum ObjectEvent
    {
        EventObjectCreate = 0x8000,
        EventObjectDestroy = 0x8001,
        EventObjectShow = 0x8002,
        EventObjectHide = 0x8003,
        EventObjectFocus = 0x8005,
        EventObjectStateChange = 0x800A,
        EventObjectLocationChange = 0x800B
    }
}
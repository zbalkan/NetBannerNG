namespace NetBannerNG.Common.Native
{
    [Flags]
    internal enum SecurityImpersonationLevel
    {
        None = 0,
        TokenDuplicate = 1 << 1,
        TokenQuery = 1 << 3,
        TokenImpersonate = 1 << 2
    }
}
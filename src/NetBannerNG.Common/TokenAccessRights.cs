namespace NetBannerNG.Common
{
    [System.Flags]
    internal enum TokenAccessRights
    {
        None = 0,
        TokenDuplicate = 1 << 1,
        TokenImpersonate = 1 << 2,
        TokenQuery = 1 << 3
    }
}

namespace NetBannerNG.Services
{
    internal sealed class FullscreenSuppressionState
    {
        public FullscreenSuppressionState(bool isSuppressed, string? appName)
        {
            IsSuppressed = isSuppressed;
            AppName = appName;
        }

        public string? AppName { get; }
        public bool IsSuppressed { get; }
    }
}
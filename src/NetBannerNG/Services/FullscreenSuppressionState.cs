namespace NetBannerNG.Services
{
    internal sealed class FullscreenSuppressionState
    {
        public FullscreenSuppressionState(bool IsSuppressed, string? AppName)
        {
            this.IsSuppressed = IsSuppressed;
            this.AppName = AppName;
        }

        public bool IsSuppressed { get; }
        public string? AppName { get; }
    }
}

namespace NetBannerNG.Utils
{
    [Serializable]
    internal class GeneralSettings
    {
        public string Classification { get; set; }
        public string BannerColor { get; set; }
        public string FontColor { get; set; }
        public int FontSize { get; set; }
        public int BannerSize { get; set; }
        public int Heartbeat { get; set; }
        public bool DisableBorders { get; set; }
    }
}
namespace NetBannerNG.Utils
{
    [Serializable]
    internal class GeneralSettings
    {
        public string Classification { get; set; }
        public string CustomBackgroundColor { get; set; }
        public string CustomForeColor { get; set; }
        public int FontSize { get; set; }
        public int BannerSize { get; set; }
        public int Heartbeat { get; set; }
        public bool DisableBorders { get; set; }

        public int InfoCon { get; set; }
        public int FpCon { get; set; }
        public string Caveats { get; set; }
        public string CustomDisplayText { get; set; }
        public bool IsPolicyManaged { get; set; }
    }
}

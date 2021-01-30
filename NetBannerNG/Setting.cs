namespace NetBannerNG
{
    public class Setting
    {
        public ClassificationMark Classification { get; set; }
        public string Caveats { get; set; }
        public ConMark ForceProtectionCon { get; set; }
        public ConMark InfoCon { get; set; }
        public CustomSettings CustomSettings { get; set; }
    }
}

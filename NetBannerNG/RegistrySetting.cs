namespace NetBannerNG
{
    public class RegistrySetting
    {
        public int Classification { get; set; }
        public int CaveatsEnabled { get; set; }
        public string Caveats { get; set; } // 40 char max
        public int? FpCon { get; set; }
        public int? InfoCon { get; set; }
        public int CustomSettings { get; set; }
        public int CustomBackgroundColor { get; set; }
        public int CustomForeColor { get; set; }
        public string CustomDisplayText { get; set; }      
    }
}
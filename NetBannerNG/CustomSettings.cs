namespace NetBannerNG
{
    public class CustomSettings
    {
        private readonly CustomBackgroundColors customBackgroundColor;
        private readonly CustomForeColors customForeColor;

        public BackgroundColor CustomBackgroundColor => new BackgroundColor((int)customBackgroundColor);
        public ForeColor CustomForeColor => new ForeColor((int)customForeColor);
        public string CustomDisplayText { get; }

        public CustomSettings(CustomBackgroundColors customBackgroundColor, CustomForeColors customForeColor, string customDisplayText)
        {
            this.customBackgroundColor = customBackgroundColor;
            this.customForeColor = customForeColor;
            CustomDisplayText = customDisplayText;
        }
    }
}

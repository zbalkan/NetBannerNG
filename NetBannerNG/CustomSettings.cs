namespace NetBannerNG
{
    public class CustomSettings
    {
        private readonly CustomBackgroundColorEnum customBackgroundColor;
        private readonly CustomForeColorEnum customForeColor;

        public BackgroundColor CustomBackgroundColor => new BackgroundColor((int)customBackgroundColor);
        public ForeColor CustomForeColor => new ForeColor((int)customForeColor);
        public string CustomDisplayText { get; }

        public CustomSettings(CustomBackgroundColorEnum customBackgroundColor, CustomForeColorEnum customForeColor, string customDisplayText)
        {
            this.customBackgroundColor = customBackgroundColor;
            this.customForeColor = customForeColor;
            CustomDisplayText = customDisplayText;
        }
    }
}

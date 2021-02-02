using System.Drawing;

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

    public enum CustomBackgroundColorEnum
    {
        Green = 1,
        Blue = 2,
        Red = 3,
        Yellow = 4,
        White = 5,
        SaddleBrown = 6,
        Purple = 7,
        Orange = 8
    }

    public enum CustomForeColorEnum
    {
        Black = 1,
        White = 2,
        Red = 3
    }
}

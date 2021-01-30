using System.Drawing;

namespace NetBannerNG
{
    public class CustomSettings
    {
        private readonly CustomBackgroundColors customBackgroundColor;
        private readonly CustomForeColors customForeColor;

        public Color CustomBackgroundColor => ConvertBackgroundColor();
        public Color CustomForeColor => ConvertForeColor();
        public string CustomDisplayText { get; }

        public CustomSettings(CustomBackgroundColors customBackgroundColor, CustomForeColors customForeColor, string customDisplayText)
        {
            this.customBackgroundColor = customBackgroundColor;
            this.customForeColor = customForeColor;
            CustomDisplayText = customDisplayText;
        }

        public Color ConvertBackgroundColor()
        {
            Color color;
            switch (customBackgroundColor)
            {
                default:
                case CustomBackgroundColors.Green:
                    color = Color.Green;
                    break;
                case CustomBackgroundColors.Blue:
                    color = Color.Blue;
                    break;
                case CustomBackgroundColors.Red:
                    color = Color.Red;
                    break;
                case CustomBackgroundColors.Yellow:
                    color = Color.Yellow;
                    break;
                case CustomBackgroundColors.White:
                    color = Color.White;
                    break;
                case CustomBackgroundColors.SaddleBrown:
                    color = Color.SaddleBrown;
                    break;
                case CustomBackgroundColors.Purple:
                    color = Color.Purple;
                    break;
                case CustomBackgroundColors.Orange:
                    color = Color.Orange;
                    break;
            }

            return color;
        }

        public Color ConvertForeColor()
        {
            Color color;
            switch (customForeColor)
            {
                default:
                case CustomForeColors.Black:
                    color = Color.Black;
                    break;
                case CustomForeColors.White:
                    color = Color.White;
                    break;
                case CustomForeColors.Red:
                    color = Color.Red;
                    break;
            }

            return color;
        }
    }

    public enum CustomBackgroundColors
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

    public enum CustomForeColors
    {
        Black = 1,
        White = 2,
        Red = 3
    }
}

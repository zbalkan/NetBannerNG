using System.Drawing;

namespace NetBannerNG
{
    public class CustomSettings
    {
        private readonly CustomBackgroundColorEnum customBackgroundColor;
        private readonly CustomForeColorEnum customForeColor;

        public Color CustomBackgroundColor => ConvertBackgroundColor();
        public Color CustomForeColor => ConvertForeColor();
        public string CustomDisplayText { get; }

        public CustomSettings(CustomBackgroundColorEnum customBackgroundColor, CustomForeColorEnum customForeColor, string customDisplayText)
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
                case CustomBackgroundColorEnum.Green:
                    color = Color.Green;
                    break;
                case CustomBackgroundColorEnum.Blue:
                    color = Color.Blue;
                    break;
                case CustomBackgroundColorEnum.Red:
                    color = Color.Red;
                    break;
                case CustomBackgroundColorEnum.Yellow:
                    color = Color.Yellow;
                    break;
                case CustomBackgroundColorEnum.White:
                    color = Color.White;
                    break;
                case CustomBackgroundColorEnum.SaddleBrown:
                    color = Color.SaddleBrown;
                    break;
                case CustomBackgroundColorEnum.Purple:
                    color = Color.Purple;
                    break;
                case CustomBackgroundColorEnum.Orange:
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
                case CustomForeColorEnum.Black:
                    color = Color.Black;
                    break;
                case CustomForeColorEnum.White:
                    color = Color.White;
                    break;
                case CustomForeColorEnum.Red:
                    color = Color.Red;
                    break;
            }

            return color;
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

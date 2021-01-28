using System.Drawing;

namespace NetBannerNG
{
    public class CustomSettings
    {
        public CustomBackgroundColor CustomBackgroundColor { get; set; }
        public CustomForeColor CustomForeColor { get; set; }
        public string CustomDisplayText { get; set; }

        public Color ConvertBackgroundColor(CustomBackgroundColor customBackgroundColor)
        {
            Color color;
            switch (customBackgroundColor)
            {
                default:
                case CustomBackgroundColor.Green:
                    color = Color.Green;
                    break;
                case CustomBackgroundColor.Blue:
                    color = Color.Blue;
                    break;
                case CustomBackgroundColor.Red:
                    color = Color.Red;
                    break;
                case CustomBackgroundColor.Yellow:
                    color = Color.Yellow;
                    break;
                case CustomBackgroundColor.White:
                    color = Color.White;
                    break;
                case CustomBackgroundColor.SaddleBrown:
                    color = Color.SaddleBrown;
                    break;
                case CustomBackgroundColor.Purple:
                    color = Color.Purple;
                    break;
                case CustomBackgroundColor.Orange:
                    color = Color.Orange;
                    break;
            }

            return color;
        }

        public Color ConvertForeColor(CustomForeColor customForeColor)
        {
            Color color;
            switch (customForeColor)
            {
                default:
                case CustomForeColor.Black:
                    color = Color.Black;
                    break;
                case CustomForeColor.White:
                    color = Color.White;
                    break;
                case CustomForeColor.Red:
                    color = Color.Red;
                    break;
            }

            return color;
        }
    }

    public enum CustomBackgroundColor
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

    public enum CustomForeColor
    {
        Black = 1,
        White = 2,
        Red = 3
    }
}

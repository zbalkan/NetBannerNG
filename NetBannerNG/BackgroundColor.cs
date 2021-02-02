using System.Drawing;

namespace NetBannerNG
{
    public class BackgroundColor
    {
        private readonly int colorValue;

        public BackgroundColor(int colorValue)
        {
            this.colorValue = colorValue;
        }

        public Color Color { get
            {
                switch (colorValue)
                {
                    default:
                    case 1:
                        return Color.Green;
                    case 2:
                        return Color.Blue;
                    case 3:
                        return Color.Red;
                    case 4:
                        return Color.Yellow;
                    case 5:
                        return Color.White;
                    case 6:
                        return Color.SaddleBrown;
                    case 7:
                        return Color.Purple;
                    case 8:
                        return Color.Orange;
                }
            }
        }
    }
}

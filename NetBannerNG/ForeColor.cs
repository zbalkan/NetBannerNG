using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace NetBannerNG
{
    public class ForeColor
    {
        private readonly int colorValue;

        public ForeColor(int colorValue)
        {
            this.colorValue = colorValue;
        }

        public Color Color
        {
            get
            {
                switch (colorValue)
                {
                    default:
                    case 1:
                        return Color.Black;
                    case 5:
                        return Color.White;
                    case 3:
                       return Color.Red;

                }
            }
        }
    }
}

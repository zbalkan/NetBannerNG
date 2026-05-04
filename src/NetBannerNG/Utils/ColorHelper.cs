using System.Globalization;
using System.Reflection;
using System.Windows.Media;

namespace NetBannerNG.Utils
{
    internal static class ColorHelper
    {
        private static readonly BrushConverter Converter = new();

        private static readonly ILookup<int, System.Drawing.Color> ColorNameLookupTable = typeof(System.Drawing.Color)
               .GetProperties(BindingFlags.Public | BindingFlags.Static)
               .Select(f => (System.Drawing.Color)f.GetValue(null, null))
               .Where(c => c.IsNamedColor)
               .ToLookup(c => c.ToArgb());

        internal static string GetColorName(SolidColorBrush solidColorBrush)
        {
            var name = solidColorBrush.Color.ToDrawingColor().ToNamedColor().Name;
            return name == "0" ? solidColorBrush.Color.ToString(CultureInfo.InvariantCulture) : name;
        }

        internal static System.Drawing.Color ToNamedColor(this System.Drawing.Color drawingColor) => ColorNameLookupTable[drawingColor.ToArgb()].FirstOrDefault();

        internal static SolidColorBrush GetColorBrush(string name) => (SolidColorBrush)Converter.ConvertFromInvariantString(name);

        internal static SolidColorBrush GetColorBrush(System.Drawing.Color drawingColor) => (SolidColorBrush)Converter.ConvertFromInvariantString(drawingColor.Name);

        internal static System.Drawing.Color ToDrawingColor(this Color mediaColor) => System.Drawing.Color.FromArgb(mediaColor.A, mediaColor.R, mediaColor.G, mediaColor.B);

        internal static Color ToMediaColor(this System.Drawing.Color drawingColor) => Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
    }
}
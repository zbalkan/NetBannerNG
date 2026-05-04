using System.Windows.Media;

namespace NetBannerNG.Utils
{
    internal static class ColorHelper
    {
        private static readonly BrushConverter Converter = new();

        internal static SolidColorBrush GetColorBrush(string name) => (SolidColorBrush)Converter.ConvertFromInvariantString(name);
    }
}

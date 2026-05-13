using System.Windows;
using System.Windows.Media;

namespace NetBannerNG.Common.AppBar
{
    public static class WPFUnitHelper
    {
        public static FontSizeConverter Converter { get; set; } = new();

        /// <summary>
        /// Transforms a coordinate from WPF space to Screen space
        /// </summary>
        /// <param name="referenceVisual">A WPF control to help conversion</param>
        /// <returns> A matrix object to Transform() </returns>
        private static Matrix WpfUnitToPixel(Visual referenceVisual)
        {
            var source = PresentationSource.FromVisual(referenceVisual);
            return source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        }

        /// <summary>
        /// Transforms a coordinate from Screen space to WPF space
        /// </summary>
        /// <param name="referenceVisual">A WPF control to help conversion</param>
        /// <returns> A matrix object to Transform() </returns>
        private static Matrix PixelToWpfUnit(Visual referenceVisual)
        {
            var source = PresentationSource.FromVisual(referenceVisual);
            return source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        }

        public static Vector Transform(Visual referenceVisual, TransformTarget target, Vector vector) => target == TransformTarget.ToPixel ? WpfUnitToPixel(referenceVisual).Transform(vector) : PixelToWpfUnit(referenceVisual).Transform(vector);

        public static Point Transform(Visual referenceVisual, TransformTarget target, Point point) => target == TransformTarget.ToPixel ? WpfUnitToPixel(referenceVisual).Transform(point) : PixelToWpfUnit(referenceVisual).Transform(point);

        public enum TransformTarget
        {
            ToPixel = 0,
            ToWpfUnit = 1,
        }
    }
}
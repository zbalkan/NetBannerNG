using System.IO;

namespace NetBannerNG.Controls.ColorPicker
{
    public static class ColorPickerSettings
    {
        private const string CustomColorsFile = "CustomColorPalette.xml";
        internal static bool UsingCustomPalette { get; set; }
        internal static string CustomColorsFilename { get; set; } = CustomColorsFile;
        internal static string CustomColorsDirectory { get; set; } = Environment.CurrentDirectory;

        public static string CustomPaletteFilename => Path.Combine(CustomColorsDirectory, CustomColorsFilename);
    }
}
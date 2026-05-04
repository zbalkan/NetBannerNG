using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Serialization;
using static System.Drawing.Color;

namespace NetBannerNG.Controls.ColorPicker.Code
{
    internal static class Util
    {
        [DllImport("shlwapi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int ColorHLSToRGB(int h, int l, int s);

        public static Color ColorFromHsl(int h, int s, int l)
        {
            var colorInt = ColorHLSToRGB(h, l, s);
            var bytes = BitConverter.GetBytes(colorInt);
            return Color.FromArgb(255, bytes[0], bytes[1], bytes[2]);
        }

        public static string ToHexString(this Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color ColorFromHexString(string hex) => Color.FromRgb(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));

        public static bool IsDialogFlagSet(this ColorPickerDialogOptions flags, ColorPickerDialogOptions flag) => (flags & flag) == flag;

        public static BitmapImage GetBitmapImage(BitmapSource bitmapSource)
        {
            var encoder = new JpegBitmapEncoder();
            var memoryStream = new MemoryStream();
            var bImg = new BitmapImage();

            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(memoryStream);

            memoryStream.Position = 0;
            bImg.BeginInit();
            bImg.StreamSource = memoryStream;
            bImg.EndInit();

            memoryStream.Close();

            return bImg;
        }

        public static float GetHue(this Color c)
        {
            var color = FromArgb(c.A, c.R, c.G, c.B);
            return color.GetHue();
        }

        public static float GetBrightness(this Color c)
        {
            var color = FromArgb(c.A, c.R, c.G, c.B);
            return color.GetBrightness();
        }

        public static float GetSaturation(this Color c)
        {
            var color = FromArgb(c.A, c.R, c.G, c.B);
            return color.GetSaturation();
        }

        public static Color FromAhsb(int alpha, float hue, float saturation, float brightness)
        {
            if (alpha is < 0 or > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha), alpha,
                    "Value must be within a range of 0 - 255.");
            }

            if (hue is < 0f or > 360f)
            {
                throw new ArgumentOutOfRangeException(nameof(hue), hue,
                    "Value must be within a range of 0 - 360.");
            }

            if (saturation is < 0f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(saturation), saturation,
                    "Value must be within a range of 0 - 1.");
            }

            if (brightness is < 0f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(brightness), brightness,
                    "Value must be within a range of 0 - 1.");
            }

            if (saturation == 0)
            {
                return Color.FromArgb((byte)alpha, Convert.ToByte(brightness * 255),
                    Convert.ToByte(brightness * 255), Convert.ToByte(brightness * 255));
            }

            float fMax;
            float fMin;

            if (brightness > 0.5)
            {
                fMax = brightness - (brightness * saturation) + saturation;
                fMin = brightness + (brightness * saturation) - saturation;
            }
            else
            {
                fMax = brightness + (brightness * saturation);
                fMin = brightness - (brightness * saturation);
            }

            var iSextant = (int)Math.Floor(hue / 60f);
            if (hue >= 300f)
            {
                hue -= 360f;
            }

            hue /= 60f;
            hue -= 2f * (float)Math.Floor((iSextant + 1f) % 6f / 2f);
            var fMid = iSextant % 2 == 0 ? (hue * (fMax - fMin)) + fMin : fMin - (hue * (fMax - fMin));
            var iMax = Convert.ToByte(fMax * 255);
            var iMid = Convert.ToByte(fMid * 255);
            var iMin = Convert.ToByte(fMin * 255);

            return iSextant switch
            {
                1 => Color.FromArgb((byte)alpha, iMid, iMax, iMin),
                2 => Color.FromArgb((byte)alpha, iMin, iMax, iMid),
                3 => Color.FromArgb((byte)alpha, iMin, iMid, iMax),
                4 => Color.FromArgb((byte)alpha, iMid, iMin, iMax),
                5 => Color.FromArgb((byte)alpha, iMax, iMin, iMid),
                _ => Color.FromArgb((byte)alpha, iMax, iMid, iMin)
            };
        }

        /// <summary>
        /// <see href="https://stackoverflow.com/questions/5091455/web-color-list-in-c-sharp-application"/>
        /// </summary>
        public static List<Color> GetWebColors() => typeof(System.Drawing.Color)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Select(info => FromName(info.Name))
                .Select(c => Color.FromArgb(c.A, c.R, c.G, c.B))
                .ToList();

        public static void SaveToXml(this ColorPalette palette, string filename) => File.WriteAllText(filename, palette.GetXmlText());

        private static string GetXmlText(this ColorPalette obj)
        {
            var xmlSerializer = new XmlSerializer(typeof(ColorPalette));

            using var sww = new StringWriter();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "    ",
                NewLineOnAttributes = false
                //OmitXmlDeclaration = true
            };
            using var writer = XmlWriter.Create(sww, settings);
            xmlSerializer.Serialize(writer, obj);
            var xml = sww.ToString();
            writer.Close();

            return xml;
        }

        public static ColorPalette LoadFromXml(this ColorPalette palette, string filename)
        {
            ColorPalette result;
            if (!File.Exists(filename))
            {
                return palette;
            }

            try
            {
                using var sr = new StreamReader(filename);
                using (var xr = new XmlTextReader(sr))
                {
                    var xmlSerializer = new XmlSerializer(typeof(ColorPalette));
                    result = (ColorPalette)xmlSerializer.Deserialize(xr);
                }

                sr.Close();
            }
            catch (Exception)
            {
                // TODO: Log error
                return palette;
            }

            return result;
        }

        public static ColorPalette LoadFromXmlText(this ColorPalette palette, string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return palette;
            }

            ColorPalette result;
            try
            {
                using var xr = XmlReader.Create(new StringReader(xml));
                var xmlSerializer = new XmlSerializer(typeof(ColorPalette));

                result = (ColorPalette)xmlSerializer.Deserialize(xr);

                xr.Close();
            }
            catch (Exception)
            {
                // TODO: Log error
                return palette;
            }

            return result;
        }
    }
}
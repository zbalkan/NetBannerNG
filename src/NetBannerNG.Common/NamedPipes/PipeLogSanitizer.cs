using System.Text;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeLogSanitizer
    {
        public static string SanitizeForSingleLineLog(string? input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(input!.Length);
            foreach (var c in input)
            {
                if (char.IsControl(c))
                {
                    sb.Append(c switch
                    {
                        '\r' => "\\r",
                        '\n' => "\\n",
                        '\t' => "\\t",
                        _ => $"\\u{(int)c:x4}"
                    });
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
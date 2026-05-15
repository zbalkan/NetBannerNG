using System.Text;

namespace NetBannerNG.Common.Extensions
{
    public static class ExceptionExtensions
    {
        private const string MessageSeparator = " ";

        public static string GetMessageStack(this Exception exception, string separator = MessageSeparator)
        {
            var result = new StringBuilder();
            var current = exception;
            var depth = 0;
            const int maxDepth = 64;

            while (current != null && depth++ < maxDepth)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    if (result.Length > 0)
                    {
                        result.Append(separator);
                    }

                    result.Append(current.Message.Trim());
                }

                current = current.InnerException;
            }

            return result.ToString();
        }
    }
}
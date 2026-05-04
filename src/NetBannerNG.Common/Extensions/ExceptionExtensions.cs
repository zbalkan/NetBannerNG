using System.Text;

namespace NetBannerNG.Common.Extensions
{
    public static class ExceptionExtensions
    {
        private const string MessageSeparator = " ";

        public static string GetMessageStack(this Exception exception, string separator = MessageSeparator)
        {
            var result = new StringBuilder();

            if (exception != default)
            {
                if (!string.IsNullOrWhiteSpace(exception.Message))
                {
                    result.Append(exception.Message.Trim());
                }

                if (exception.InnerException != default)
                {
                    var furtherMessages = exception.InnerException.GetMessageStack();

                    if (!string.IsNullOrWhiteSpace(furtherMessages))
                    {
                        if (result.Length > 0)
                        {
                            result.Append(separator);
                        }

                        result.Append(furtherMessages.Trim());
                    }
                }
            }

            return result.ToString();
        }
    }
}
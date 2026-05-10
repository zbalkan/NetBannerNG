namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeNaming
    {
        private const string Prefix = "netbannerng-pipe-s";

        [CLSCompliant(false)]
        public static string ForSession(uint sessionId) => $"{Prefix}{sessionId}";

        public static bool TryParsePipeName(string? value, out string pipeName)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                pipeName = value!.Trim();
                return true;
            }

            pipeName = string.Empty;
            return false;
        }
    }
}
namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeMessageValidator
    {
        public const int MaxLogTextLength = 4096;

        public static bool IsValidInboundClientMessage(PipeMessage message)
        {
            if (message == null)
            {
                return false;
            }

            if (message.Action != ActionType.SendLog)
            {
                return false;
            }

            if (!PipeMessageChecksum.IsValid(message))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(message.Text) && message.Text?.Length <= MaxLogTextLength;
        }
    }
}
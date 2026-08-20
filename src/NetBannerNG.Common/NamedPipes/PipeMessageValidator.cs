namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeMessageValidator
    {
        public const int MaxLogTextLength = 4096;

        public static bool IsValidInboundClientMessage(PipeMessage? message)
        {
            if (message == null)
            {
                return false;
            }

            if (!PipeMessageChecksum.IsValid(message))
            {
                return false;
            }

            if (message.Action == ActionType.Ready)
            {
                // Keep the readiness protocol explicit and versioned so an accidental
                // or future message shape cannot satisfy the watchdog health gate.
                return message.Text == "1";
            }

            if (message.Action != ActionType.SendLog)
            {
                return false;
            }

#pragma warning disable CA1508 // Avoid dead conditional code
            return !string.IsNullOrWhiteSpace(message.Text) && message.Text?.Length <= MaxLogTextLength;
#pragma warning restore CA1508 // Avoid dead conditional code
        }
    }
}
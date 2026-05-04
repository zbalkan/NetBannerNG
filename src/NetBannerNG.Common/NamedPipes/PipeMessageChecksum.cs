using System.Security.Cryptography;
using System.Text;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeMessageChecksum
    {
        public const int ChecksumLengthBytes = 32;

        public static byte[] Compute(PipeMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            var payload = $"{message.Id:N}|{(int)message.Action}|{message.Text ?? string.Empty}";
            return SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        }

        public static bool IsValid(PipeMessage message)
        {
            if (message == null || message.Checksum == null || message.Checksum.Length != ChecksumLengthBytes)
            {
                return false;
            }

            var expected = Compute(message);
            return CryptographicOperations.FixedTimeEquals(expected, message.Checksum);
        }
    }
}

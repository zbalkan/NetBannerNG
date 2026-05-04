using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeMessageChecksum
    {
        public const int ChecksumLengthBytes = 32;
        private static readonly SHA256 SHA256 = SHA256.Create();

        public static byte[] Compute(PipeMessage message)
        {
            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var payload = $"{message.Id:N}|{(int)message.Action}|{message.Text ?? string.Empty}";
            return SHA256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        public static bool IsValid(PipeMessage message)
        {
            if (message == null || message.Checksum == null || message.Checksum.Length != ChecksumLengthBytes)
            {
                return false;
            }

            var expected = Compute(message);
            return FixedTimeEquals(expected, message.Checksum);
        }

        /// <summary>
        /// Compares two byte arrays in constant time.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoOptimization)] // Prevent compiler shortcuts
        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null) return false;
            if (left.Length != right.Length) return false;

            int accum = 0;
            for (int i = 0; i < left.Length; i++)
            {
                accum |= left[i] ^ right[i];
            }

            return accum == 0;
        }
    }
}
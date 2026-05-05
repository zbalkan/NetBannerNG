using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace NetBannerNG.Common.NamedPipes
{
    public static class PipeMessageChecksum
    {
        public const int ChecksumLengthBytes = 32;

        private const byte PayloadVersion = 1;

        private static readonly ThreadLocal<SHA256> Sha256 =
            new ThreadLocal<SHA256>(SHA256.Create);

        public static byte[] Compute(PipeMessage message) => ComputeHash(message);

        public static bool IsValid(PipeMessage message)
        {
            if (message == null ||
                message.Checksum == null ||
                message.Checksum.Length != ChecksumLengthBytes)
            {
                return false;
            }

            var expected = ComputeHash(message);

            try
            {
                return FixedTimeEquals(expected, message.Checksum);
            }
            finally
            {
                Array.Clear(expected, 0, expected.Length);
            }
        }

        private static byte[] ComputeHash(PipeMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var text = message.Text ?? string.Empty;
            var textByteCount = Encoding.UTF8.GetByteCount(text);

            // PayloadVersion + Guid + Action(Int32 LE) + UTF8(Text)
            var payloadLength = 1 + 16 + 4 + textByteCount;

            var payload = ArrayPool<byte>.Shared.Rent(payloadLength);

            try
            {
                var offset = 0;

                payload[offset++] = PayloadVersion;

                var guidBytes = message.Id.ToByteArray();
                Buffer.BlockCopy(guidBytes, 0, payload, offset, 16);
                offset += 16;

                WriteInt32LittleEndian(payload, offset, (int)message.Action);
                offset += 4;

                offset += Encoding.UTF8.GetBytes(
                    text,
                    0,
                    text.Length,
                    payload,
                    offset);

                return Sha256.Value.ComputeHash(payload, 0, offset);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(payload);
            }
        }

        private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var diff = 0;

            for (var i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }
    }
}
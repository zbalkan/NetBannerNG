using MessagePack;

namespace NetBannerNG.Common.NamedPipes
{
    /// <summary>
    /// An instance of a formatted named pipe message
    /// <see href="https://erikengberg.com/named-pipes-in-net-6-with-tray-icon-and-service/"/>
    /// </summary>
    [MessagePackObject]
    [Serializable]
    public class PipeMessage
    {
        private readonly byte[] _checksum = new byte[32];

        public PipeMessage()
        {
            Id = Guid.NewGuid();
        }

        [Key(0)]
        public Guid Id { get; set; }

        [Key(1)]
        public ActionType Action { get; set; }

        [Key(2)]
        public string Text { get; set; }

        [Key(3)]
#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] Checksum
        {
            get
            {
                return (byte[])_checksum.Clone();
            }
            set
            {
                if (value == null || value.Length != PipeMessageChecksum.ChecksumLengthBytes)
                {
                    throw new ArgumentException($"Checksum must be {PipeMessageChecksum.ChecksumLengthBytes} bytes long.", nameof(value));
                }
                Array.Copy(value, _checksum, PipeMessageChecksum.ChecksumLengthBytes);
            }
        }
#pragma warning restore CA1819 // Properties should not return arrays
    }
}

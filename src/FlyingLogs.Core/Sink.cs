using System.Numerics;

namespace FlyingLogs.Core
{
    public abstract class Sink
    {
        public LogEncodings ExpectedEncoding { get; private set; }

        public Sink(LogEncodings expectedEncoding)
        {
            if (BitOperations.PopCount(unchecked((uint)expectedEncoding)) != 1)
            {
                throw new ArgumentException(
                    "A sink can only support one encoding at a time.",
                    nameof(expectedEncoding));
            }

            ExpectedEncoding = expectedEncoding;
        }

        public abstract void Ingest(
            LogTemplate template,
            IReadOnlyList<ReadOnlyMemory<byte>> propertyValues,
            Memory<byte> temporaryBuffer);
    }
}

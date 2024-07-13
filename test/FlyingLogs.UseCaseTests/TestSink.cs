using FlyingLogs.Core;

namespace FlyingLogs.UseCaseTests
{
    /// <summary>
    /// A simple sink that moves all the functionality to provided delegates to allow customization for testing.
    /// </summary>
    internal class TestSink : Sink
    {
        public Action<LogTemplate, IReadOnlyList<ReadOnlyMemory<byte>>> OnIngest { get; set; }
            = (template, values) => { };

        public TestSink(LogEncodings encoding) : base(encoding) { }

        public override void Ingest(
            LogTemplate template,
            IReadOnlyList<ReadOnlyMemory<byte>> values,
            Memory<byte> temporaryBuffer)
        {
            OnIngest(template, values);
        }
    }
}

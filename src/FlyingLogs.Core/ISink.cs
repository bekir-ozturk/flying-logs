namespace FlyingLogs.Core
{
    public interface ISink
    {
        LogEncodings ExpectedEncoding { get; }
        void Ingest(RawLog log);
    }
}

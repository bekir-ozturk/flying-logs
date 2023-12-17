using FlyingLogs.Shared;

namespace FlyingLogs.Core
{
    public interface ISink
    {
        LogEncoding ExpectedEncoding { get; }
        void Ingest(RawLog log);
        bool IsLogLevelActive(LogLevel level);
    }
}

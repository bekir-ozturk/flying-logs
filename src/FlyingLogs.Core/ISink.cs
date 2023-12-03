using FlyingLogs.Shared;

namespace FlyingLogs.Core
{
    public interface ISink
    {
        void Ingest(RawLog log);
        bool IsLogLevelActive(LogLevel level);
    }
}

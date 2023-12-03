using FlyingLogs.Shared;

namespace FlyingLogs.Core
{
    public interface ISink
    {
        void Ingest(RawLog log);
        void IsLogLevelActive(LogLevel level);
    }
}

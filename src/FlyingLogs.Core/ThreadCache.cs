namespace FlyingLogs.Core
{
    public class ThreadCache
    {
        public static readonly ThreadLocal<Memory<byte>> Buffer = new ThreadLocal<Memory<byte>>(() => new byte[4096]);
        public static readonly ThreadLocal<RawLog> RawLog = new ThreadLocal<RawLog>(() => new RawLog());
    }
}

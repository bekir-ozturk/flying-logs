namespace FlyingLogs.Core
{
    public class ThreadCache
    {
        public static readonly ThreadLocal<Memory<byte>> Buffer = new ThreadLocal<Memory<byte>>(() => new byte[4096]);
        
        /// <summary>
        /// Provides a <see cref="RawLog"/> instance to each calling thread.
        /// </summary>
        public static readonly ThreadLocal<RawLog> RawLog = new ThreadLocal<RawLog>(() => new RawLog());
    }
}

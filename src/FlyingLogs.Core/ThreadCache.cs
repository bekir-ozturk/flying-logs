namespace FlyingLogs.Core
{
    public class ThreadCache
    {
        public static int BufferSize = 4096;

        public static readonly ThreadLocal<Memory<byte>> Buffer = new ThreadLocal<Memory<byte>>(() => new byte[BufferSize]);
        
        /// <summary>
        /// Provides a <see cref="RawLog"/> instance to each calling thread.
        /// </summary>
        public static readonly ThreadLocal<RawLog> RawLog = new ThreadLocal<RawLog>(() => new RawLog());

        /// <summary>
        /// Provides a <see cref="RawLog"/> instance to each calling thread to be used when encoding a log event into
        /// another encoding at runtime. This is only needed if an assembly was compiled without preencoding and
        /// the encoding should be done at runtime instead.
        /// </summary>
        public static readonly ThreadLocal<RawLog> RawLogForReencoding = new ThreadLocal<RawLog>(() => new RawLog());
    }
}

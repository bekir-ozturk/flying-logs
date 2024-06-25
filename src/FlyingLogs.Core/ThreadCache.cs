namespace FlyingLogs.Core
{
    public class ThreadCache
    {
        public static int BufferSize = 4096;

        public static readonly ThreadLocal<Memory<byte>> Buffer = new ThreadLocal<Memory<byte>>(() => new byte[BufferSize]);
        
        /// <summary>
        /// Provides a preallocated list to store the values of properties in a log event. Each thread has its own list
        /// to avoid contention.
        /// </summary>
        public static readonly ThreadLocal<List<ReadOnlyMemory<byte>>> PropertyValuesTemp = new (() => new (32));
    }
}

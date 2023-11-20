namespace FlyingLogs.Core
{
    public interface ILogSink
    {
        static ILogSink Instance { get; }
        Memory<byte> PeekBufferSpaceForThread(int size);
        void CommitBufferSpaceForThread(int usedSize);
    }
}

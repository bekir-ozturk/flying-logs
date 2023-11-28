namespace FlyingLogs.Core
{
    public interface ISink
    {
        static ISink Instance { get; }
        Memory<byte> PeekBufferSpaceForThread(int size);
        void CommitBufferSpaceForThread(int usedSize);
    }
}

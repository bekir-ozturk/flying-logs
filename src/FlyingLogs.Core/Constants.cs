namespace FlyingLogs.Core;

public static class Constants
{
    public static readonly ReadOnlyMemory<ReadOnlyMemory<byte>> LogLevelsUtf8Plain = new ReadOnlyMemory<byte>[]
    {
        "Trace"u8.ToArray(),          // Trace = 0,
        "Debug"u8.ToArray(),          // Debug = 1,
        "Information"u8.ToArray(),    // Information = 2,
        "Warning"u8.ToArray(),        // Warning = 3,
        "Error"u8.ToArray(),          // Error = 4,
        "Critical"u8.ToArray(),       // Critical = 5,
        "None"u8.ToArray(),           // None = 6,
    };

    // Bytes for the log levels are the same when encoded to Json.
    public static readonly ReadOnlyMemory<ReadOnlyMemory<byte>> LogLevelsUtf8Json = LogLevelsUtf8Plain;
}
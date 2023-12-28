using System;

namespace FlyingLogs.Core
{
    [Flags]
    public enum LogEncodings
    {
        None = 0,
        Utf8Plain = 1 << 0,
        Utf8Json = 1 << 1,
    }
}
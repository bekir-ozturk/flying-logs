using System;
using System.Collections.Generic;

namespace FlyingLogs.Shared
{
    public static class Encoded
    {
        // UTF8 encoded strings of format: ,"@l":"Error"
        // These can be directly copied into clef buffer.
        public static readonly Memory<byte> _uCommaAndLevelCritical = new byte[] { 44, 34, 64, 108, 34, 58, 34, 67, 114, 105, 116, 105, 99, 97, 108, 34, };
        public static readonly Memory<byte> _uCommaAndLevelError = new byte[] { 44, 34, 64, 108, 34, 58, 34, 69, 114, 114, 111, 114, 34, };
        public static readonly Memory<byte> _uCommaAndLevelWarning = new byte[] { 44, 34, 64, 108, 34, 58, 34, 87, 97, 114, 110, 105, 110, 103, 34, };
        public static readonly Memory<byte> _uCommaAndLevelInformation = new byte[] { 44, 34, 64, 108, 34, 58, 34, 73, 110, 102, 111, 114, 109, 97, 116, 105, 111, 110, 34, };
        public static readonly Memory<byte> _uCommaAndLevelDebug = new byte[] { 44, 34, 64, 108, 34, 58, 34, 68, 101, 98, 117, 103, 34, };
        public static readonly Memory<byte> _uCommaAndLevelTrace = new byte[] { 44, 34, 64, 108, 34, 58, 34, 84, 114, 97, 99, 101, 34, };

        public static readonly Dictionary<LogLevel, Memory<byte>> JsonPartsLevel = new Dictionary<LogLevel, Memory<byte>>()
        {
            { LogLevel.Critical, _uCommaAndLevelCritical },
            { LogLevel.Error, _uCommaAndLevelError },
            { LogLevel.Warning, _uCommaAndLevelWarning },
            { LogLevel.Information, _uCommaAndLevelInformation },
            { LogLevel.Debug, _uCommaAndLevelDebug },
            { LogLevel.Trace, _uCommaAndLevelTrace  },
        };
    }
}

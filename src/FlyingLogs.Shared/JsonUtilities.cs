using System;
using System.Collections.Generic;

namespace FlyingLogs.Shared;

public static class JsonUtilities
{
    public static bool JsonEncodePropertyValues(
        List<ReadOnlyMemory<byte>> values,
        Memory<byte> buffer,
        ref int offset)
    {
        bool failed = false;
        int valueCount = values.Count;
        for (int i = 0; i < valueCount; i++)
        {
            failed |= System.Text.Encodings.Web.JavaScriptEncoder.Default.EncodeUtf8(
                values[i].Span,
                buffer.Span.Slice(offset),
                out int _,
                out int bytesWritten) != System.Buffers.OperationStatus.Done;

            values[i] = buffer.Slice(offset, bytesWritten);
            offset += bytesWritten;
        }

        return !failed;
    }
}

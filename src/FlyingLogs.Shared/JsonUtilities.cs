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
            if (values[i].Length == 0)
            {
                // Zero length values are very likely PropertyValueHints. PropertyValueHints are compared by reference.
                // We can't reencode these as reference equality would be broken for the encoded copy.
                // For regular zero-length strings, there is nothing to encode anyway. Skip.
                continue;
            }

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

using System;
using System.Collections.Generic;

namespace FlyingLogs.Shared;

public static class JsonUtilities
{
    /// <summary>
    /// Given a list of property values, JSON encodes them and places the new values into
    /// <paramref name="jsonEncodedValues"/>. Only encodes properties with type String. The others are copied from the
    /// <paramref name="values"> directly.
    /// </summary>
    /// <param name="values">Utf8 strings to be encoded.</param>
    /// <param name="types">Types of the properties. Only string properties will be encoded.</param>
    /// <param name="buffer">Temporary buffer to be used for the newly encoded strings.</param>
    /// <param name="offset">Offset in the buffer pointing to the next available byte.</param>
    /// <param name="jsonEncodedValues">Resulting Utf8 string enoded to JSON.</param>
    /// <returns></returns>
    public static bool JsonEncodePropertyValues(
        IReadOnlyList<ReadOnlyMemory<byte>> values,
        ReadOnlyMemory<BasicPropertyType> types,
        Memory<byte> buffer,
        ref int offset,
        List<ReadOnlyMemory<byte>> jsonEncodedValues)
    {
        bool failed = false;
        int valueCount = values.Count;
        jsonEncodedValues.Clear();
        for (int i = 0; i < valueCount; i++)
        {
            if (values[i].Length == 0)
            {
                // Zero length values are very likely PropertyValueHints. PropertyValueHints are compared by reference.
                // We can't reencode these as reference equality would be broken for the encoded copy.
                // For regular zero-length strings, there is nothing to encode anyway. Just reuse the old value.
                jsonEncodedValues.Add(values[i]);
                continue;
            }

            if (types.Span[i] != BasicPropertyType.String)
            {
                // We assume that anything other than a BasicPropertyType.String is already JSON compliant.
                // No encoding needed.
                jsonEncodedValues.Add(values[i]);
                continue;
            }

            failed |= System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping.EncodeUtf8(
                values[i].Span,
                buffer.Span.Slice(offset),
                out int _,
                out int bytesWritten) != System.Buffers.OperationStatus.Done;

            jsonEncodedValues.Add(buffer.Slice(offset, bytesWritten));
            offset += bytesWritten;
        }

        return !failed;
    }
}

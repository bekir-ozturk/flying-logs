using System.Collections.Concurrent;
using System.Text.Encodings.Web;

namespace FlyingLogs.Core
{
    internal class Reencoder
    {
        /// <summary>
        /// Given the message template encoded as Utf8Plain, returns the pieces encoded as Utf8Json. Note that
        /// Memory<T> type handles equality comparisons by using the "memory address" and the length, and not by
        /// content. This means the same string used in different assemblies may be duplicated in this dictionary, but
        /// any comparisons that the dictionary will make will be much cheaper since we won't iterate over the whole
        /// memory.
        /// </summary>
        private static readonly ConcurrentDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<ReadOnlyMemory<byte>>> JsonEncodedPieces = new();

        /// <summary>
        /// Given a string encoded as Utf8Plain, returns the same string encoded as Utf8Json. Note that Memory<T> type
        /// handles equality comparisons by using the "memory address" and the length, and not by content. This means
        /// the same string used in different assemblies may be duplicated in this dictionary, but any comparisons that
        /// the dictionary will make will be much cheaper since we won't iterate over the whole memory.
        /// </summary>
        private static readonly ConcurrentDictionary<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>> JsonEncodedStrings = new();

        /// <summary>
        /// Buffer to initially encode the string into. Dotnet libraries today don't expose an API to calculate the
        /// length of the json encoded string before doing the actual encoding. Therefore, we don't know how much to
        /// allocate. This buffer allows us to encode first and learn the size at the same time; then copy over.
        /// </summary>
        private static readonly ThreadLocal<Memory<byte>> TmpBuffer = new ThreadLocal<Memory<byte>>(() => new byte[4096]);


        /// <summary>
        /// Re-encodes the given Utf8Plain log data into a Ut8Json one.
        /// </summary>
        /// <param name="utf8Plain">Original data to reencode.</param>
        /// <param name="utf8Json">Target log object to contain the json encoded data.</param>
        /// <param name="tmpBuffer">Preallocated memory to be used for the generated json data.
        /// This buffer shouldn't be cached as it is only borrowed until this method returns.</param>
        /// <returns>The number of bytes used from the <see cref="tmpBuffer"/>.</returns>
        public static int ReencodeUtf8PlainToUtf8Json(RawLog utf8Plain, RawLog utf8Json, Memory<byte> tmpBuffer)
        {
            int tmpBufferUsedBytes = 0;

            // Copy fields that can't change with json encoding.
            utf8Json.Level = utf8Plain.Level;
            utf8Json.Encoding = utf8Plain.Encoding;
            utf8Json.BuiltinProperties[(int)BuiltInProperty.Timestamp] = utf8Plain.BuiltinProperties[(int)BuiltInProperty.Timestamp];
            utf8Json.BuiltinProperties[(int)BuiltInProperty.Level] = utf8Plain.BuiltinProperties[(int)BuiltInProperty.Level];
            utf8Json.BuiltinProperties[(int)BuiltInProperty.EventId] = utf8Plain.BuiltinProperties[(int)BuiltInProperty.EventId];

            // Encode pieces to json if not already done.
            ReadOnlyMemory<byte> template = utf8Plain.BuiltinProperties[(int)BuiltInProperty.Template];
            if (JsonEncodedPieces.TryGetValue(template, out var cachedPieces) == false)
            {
                ReadOnlyMemory<byte>[] freshlyEncodedPieces = new ReadOnlyMemory<byte>[utf8Plain.MessagePieces.Length];
                for (int i = 0; i < utf8Plain.MessagePieces.Length; i++)
                {
                    freshlyEncodedPieces[i] = JsonEncodeWithAllocate(utf8Plain.MessagePieces.Span[i]);
                }

                JsonEncodedPieces[template] = cachedPieces = freshlyEncodedPieces;
            }

            utf8Json.MessagePieces = cachedPieces;

            // Encode property names.
            /* We can't cache these based on the template. Multiple events may share the same template but have
             * different property names. This is due to property expansion (using @) where depending on the type of
             * the expanded property, the generated property name may be different. Additional properties and assembly
             * level properties are other examples why template cannot be used to cache property names. */
            utf8Json.Properties.Clear();
            for (int i=0; i < utf8Plain.Properties.Count; i++)
            {
                (var name, var value) = utf8Plain.Properties[i];
                if (JsonEncodedStrings.TryGetValue(name, out ReadOnlyMemory<byte> jsonEncodedName) == false)
                {
                    JsonEncodedStrings[name] = jsonEncodedName = JsonEncodeWithAllocate(name);
                }

                // Don't look for the value in the cache. Values are dynamic and wouldn't benefit much from the cache.
                var valueEncodeResult = JavaScriptEncoder.Default.EncodeUtf8(
                        value.Span,
                        tmpBuffer.Span,
                        out int _,
                        out int valueJsonBytes);

                ReadOnlyMemory<byte> jsonEncodedValue;
                if (valueEncodeResult == System.Buffers.OperationStatus.Done)
                {
                    jsonEncodedValue = tmpBuffer.Slice(0, valueJsonBytes);
                    tmpBufferUsedBytes += valueJsonBytes;
                    tmpBuffer = tmpBuffer.Slice(valueJsonBytes);
                }
                else
                {
                    // TODO emit metric
                    jsonEncodedValue = Memory<byte>.Empty;
                }

                utf8Json.Properties.Add((jsonEncodedName, jsonEncodedValue));
            }

            return tmpBufferUsedBytes;
        }

        private static ReadOnlyMemory<byte> JsonEncodeWithAllocate(ReadOnlyMemory<byte> source)
        {
            int jsonByteCount;
            while (true)
            {
                var result = JavaScriptEncoder.Default.EncodeUtf8(
                    source.Span,
                    TmpBuffer.Value.Span,
                    out int _,
                    out jsonByteCount);

                if (result == System.Buffers.OperationStatus.DestinationTooSmall)
                {
                    TmpBuffer.Value = new byte[TmpBuffer.Value.Length * 2];
                    continue;
                }
                else
                {
                    if (result != System.Buffers.OperationStatus.Done)
                    {
                        jsonByteCount = 0;
                    }
                    break;
                }
            }

            byte[] destination = new byte[jsonByteCount];
            TmpBuffer.Value.Span.Slice(0, jsonByteCount).CopyTo(destination);
            return destination;
        }
    }
}

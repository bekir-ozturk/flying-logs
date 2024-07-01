using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace FlyingLogs.Core;

internal static class JsonUtilities
{
    /// <summary>
    /// Buffer to initially encode the string into. Dotnet libraries today don't expose an API to calculate the
    /// length of the json encoded string before doing the actual encoding. Therefore, we don't know how much to
    /// allocate. This buffer allows us to encode first and learn the size at the same time; then copy over.
    /// </summary>
    private static readonly ThreadLocal<Memory<byte>> TmpBuffer = new ThreadLocal<Memory<byte>>(() => new byte[4096]);

    private static readonly ConcurrentDictionary<LogTemplate, LogTemplate> Utf8PlainToUtf8JsonEncodedTemplates = new ();

    public static LogTemplate GetUtf8JsonEncodedTemplate(LogTemplate utf8PlainTemplate)
    {
        if (Utf8PlainToUtf8JsonEncodedTemplates.TryGetValue(utf8PlainTemplate, out LogTemplate? utf8JsonEncoded))
        {
            return utf8JsonEncoded;
        }

        utf8JsonEncoded = EncodeLogTemplateToUtf8Json(utf8PlainTemplate);
        Utf8PlainToUtf8JsonEncodedTemplates[utf8PlainTemplate] = utf8JsonEncoded;
        return utf8JsonEncoded;
    }

    private static LogTemplate EncodeLogTemplateToUtf8Json(LogTemplate utf8PlainTemplate)
    {
        ReadOnlyMemory<byte>[] messagePieces = new ReadOnlyMemory<byte>[utf8PlainTemplate.MessagePieces.Length];
        for (int i=0; i<messagePieces.Length; i++)
            messagePieces[i] = JsonEncodeWithAllocate(utf8PlainTemplate.MessagePieces.Span[i]);

        ReadOnlyMemory<byte>[] propertyNames = new ReadOnlyMemory<byte>[utf8PlainTemplate.PropertyNames.Length];
        for (int i=0; i<propertyNames.Length; i++)
            propertyNames[i] = JsonEncodeWithAllocate(utf8PlainTemplate.PropertyNames.Span[i]);

        return new LogTemplate(
            utf8PlainTemplate.Level,
            utf8PlainTemplate.EventId,
            JsonEncodeWithAllocate(utf8PlainTemplate.TemplateString),
            messagePieces,
            propertyNames,
            utf8PlainTemplate.PropertyTypes,
            utf8PlainTemplate.PropertyDepths
        );
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
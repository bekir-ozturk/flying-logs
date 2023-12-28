using System.Text;

using FlyingLogs.Core;

namespace FlyingLogs.Sinks;

public sealed class ConsoleSink : ISink
{
    public LogEncodings ExpectedEncoding => LogEncodings.Utf8Plain;

    private readonly Stream _consoleOut;
    private readonly ReadOnlyMemory<byte> _uNewLine = Encoding.UTF8.GetBytes(Environment.NewLine);
    private readonly ThreadLocal<Memory<byte>> _buffer = new (() => new byte[ThreadCache.BufferSize]);

    public ConsoleSink()
    {
        Console.OutputEncoding = Encoding.UTF8;
        _consoleOut = Console.OpenStandardOutput();
    }

    public void Ingest(RawLog log)
    {
        var s = _buffer.Value.Span;

        CopyToAndMoveDestination(log.BuiltinProperties[(int)BuiltInProperty.Timestamp].Span, ref s);
        CopyToAndMoveDestination(" "u8, ref s);
        CopyToAndMoveDestination(log.BuiltinProperties[(int)BuiltInProperty.Level].Span, ref s);
        CopyToAndMoveDestination(" "u8, ref s);

        // Leave the last piece out; its special.
        for (int i = 0; i < log.MessagePieces.Length - 1; i++)
        {
            CopyToAndMoveDestination(log.MessagePieces.Span[i].Span, ref s);
            CopyToAndMoveDestination(log.Properties[i].value.Span, ref s);
        }
        // Print last piece alone; no property.
        CopyToAndMoveDestination(log.MessagePieces.Span[log.MessagePieces.Length - 1].Span, ref s);

        for (int i = log.MessagePieces.Length - 1; i < log.Properties.Count; i++)
        {
            CopyToAndMoveDestination(" "u8, ref s);
            CopyToAndMoveDestination(log.Properties[i].name.Span, ref s);
            CopyToAndMoveDestination(":"u8, ref s);
            CopyToAndMoveDestination(log.Properties[i].value.Span, ref s);
        }
        CopyToAndMoveDestination(_uNewLine.Span, ref s);

        int totalLength = _buffer.Value.Length - s.Length;
        _consoleOut.Write(s.Slice(0, totalLength));
    }

    private static void CopyToAndMoveDestination(ReadOnlySpan<byte> source, ref Span<byte> destination)
    {
        source.CopyTo(destination);
        destination = destination.Slice(source.Length);
    }
}
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.Sinks;

public sealed class ConsoleSink : ISink
{
    public LogLevel MinimumLogLevel { get; set; }

    private readonly Stream _consoleOut;
    private readonly ReadOnlyMemory<byte> _uNewLine = Encoding.UTF8.GetBytes(Environment.NewLine);

    public ConsoleSink() : this(LogLevel.Information) { }

    public ConsoleSink(LogLevel minimumLogLevel)
    {
        MinimumLogLevel = minimumLogLevel;
        Console.OutputEncoding = Encoding.UTF8;
        _consoleOut = Console.OpenStandardOutput();
    }

    // TODO: this needs to be made thread safe.
    public void Ingest(RawLog log)
    {
        _consoleOut.Write(log.Properties[(int)LogProperty.Timestamp].value.Span);
        _consoleOut.Write(" "u8);
        _consoleOut.Write(log.Properties[(int)LogProperty.Level].value.Span);
        _consoleOut.Write(" "u8);

        // Leave the last piece out; its special.
        for (int i=0; i < log.MessagePieces.Length - 1; i++)
        {
            _consoleOut.Write(log.MessagePieces.Span[i].Span);
            _consoleOut.Write(log.Properties[i + log.PositionalPropertiesStartIndex].value.Span);
        }
        // Print last piece alone; no property.
        _consoleOut.Write(log.MessagePieces.Span[log.MessagePieces.Length - 1].Span);

        for (int i = log.AdditionalPropertiesStartIndex; i < log.Properties.Count; i++)
        {
            _consoleOut.Write(" "u8);
            _consoleOut.Write(log.Properties[i].name.Span);
            _consoleOut.Write(":"u8);
            _consoleOut.Write(log.Properties[i].value.Span);
        }
        _consoleOut.Write(_uNewLine.Span);
    }

    public bool IsLogLevelActive(LogLevel level)
    {
        return level >= MinimumLogLevel;
    }
}
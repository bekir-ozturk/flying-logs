using System.Text;

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

public class DummySerilogClefSink : ILogEventSink
{
    private readonly CompactJsonFormatter _formatter = new CompactJsonFormatter();

    private readonly byte[] _buffer;
    private readonly StreamWriter _textWriter;

    public DummySerilogClefSink()
    {
        _buffer = new byte[8 * 1024];
        _textWriter = new StreamWriter(new MemoryStream(_buffer));
    }

    public void Emit(LogEvent logEvent)
    {
        // Reuse the same memory block to avoid creating StreamWriters.
        _textWriter.BaseStream.Seek(0, SeekOrigin.Begin);
        _formatter.Format(logEvent, _textWriter);
        // Move data to its destination: _buffer
        _textWriter.Flush();
    }
}
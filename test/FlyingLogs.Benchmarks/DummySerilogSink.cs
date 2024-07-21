using Serilog.Core;
using Serilog.Events;

public class DummySerilogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {

    }
}
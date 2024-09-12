using NLog;
using NLog.Targets;

[Target("Null")]
public sealed class DummyNlogSink: TargetWithLayout
{
    protected override void Write(LogEventInfo logEvent) {
        logEvent = logEvent;
    }
}
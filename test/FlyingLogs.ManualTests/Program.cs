// See https://aka.ms/new-console-template for more information
using FlyingLogs;
using FlyingLogs.Shared;
using FlyingLogs.Sinks;

Console.WriteLine("Hello, World!");

Configuration.Initialize(
    new ConsoleSink(LogLevel.Trace),
    new SeqHttpSink(LogLevel.Trace, "raspberrypi.local", 5002)
);

for (int i=0; i<30000; i++)
{
    Log.Error.L0("Hello, {subject}!", i);
    Log.Warning.L1("This just raised some suspicion, sorry.");
    Log.Trace.L2("He's making up for it since the last {delta} seconds", i * 0.02f + 5);
    Log.Critical.L3("Some player with id {playerId} has blocked the {mapName} map for {delay} seconds.", 40302, "tundra", i/1000.0);
}

await (Configuration.Current.Sinks.First(s => s.sink is SeqHttpSink).sink as SeqHttpSink)!.DrainAsync();
Console.WriteLine("Completely drained.");
// See https://aka.ms/new-console-template for more information
using FlyingLogs;
using FlyingLogs.Shared;
using FlyingLogs.Sinks;

Console.WriteLine("Hello, World!");

Configuration.Initialize((LogLevel.Trace, new ConsoleSink()));

for (int i=0; i<30000; i++)
{
    Log.Error.L0("Hello, {subject}!", 3);
    Log.Warning.L1("This just raised some suspicion, sorry.");
    Log.Trace.L2("He's making up for it since the last {delta} seconds", 5);
}
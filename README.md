# flying-logs
A high performance, minimal allocation logging library.

## Why choose flying-logs?
flying-logs was initially developed with game servers in mind. In such scenarios, the server is expected to process the entire world 10 to 60 times per second depending on the game. Such high frequency leaves as low as ~16 milliseconds to handle all the game logic, network calls, metric emissions, and of course, logging.

Although C# & .NET make it not only possible but also very easy to write high performance code, there is one thing that might put your high frame rate at risk: garbage collector. Depending on how much you allocate, GC can suspend your threads for hundreds of milliseconds, putting you way behind your 16ms frame time goal.

To avoid such spikes, many game developers keep allocations low by preloading their resources at launch or by using object pooling. While these get us far in keeping the allocations low, they won't be sufficient if our logging library keeps allocating strings every frame.

With these concerns in mind, flying-logs were created. It makes no allocations in a significant majority of the cases, all the parsing is done at compile time, and it has a very thin abstraction layer to get your logs to their destination as fast as possible.

## How is high performance achieved?
flying-logs is all about source generators. It is empowered by the fact that most of what is to be logged is already known at compile time. The message template in your log, names of the positional properties, log levels, event ids and any other string that is known at compile time is pre-encoded to Utf8 and stored in byte arrays. At runtime, the task of the sinks is to just copy these memory sections into the target stream in the correct order. Values of your properties, which are not known at compile time, are efficiently encoded at runtime to a preallocated memory block to be used by sinks. flying-logs utilize the latest `IUtf8SpanFormattable` APIs to encode the most common types without ever calling `ToString()` on them. Any string that needs to be allocated is quickly discarded after use to decrease the chance of it surviving a Gen0 collection by the GC.

## Getting Started

To get started, you need the source generator package in your project:

```
dotnet add package FlyingLogs
```

With this package alone we are ready to log messages, but they won't show up anywhere until we configure some sinks. Console sink is already included in the `FlyingLogs` package. If you want `File` or `Seq` sinks, you can add `FlyingLogs.Sinks.File` and `FlyingLogs.Sinks.Seq` packages respectively.

```csharp
FlyingLogs.Configuration.Initialize(new ConsoleSink());
```

> [!CAUTION]
> ConsoleSink is only intended for debugging scenarios and shouldn't be used in production environments.

To log some event, simply start by calling an imaginary log method as below. The name of the method is up to you, but it needs to be unique within the assembly. It is recommended to pick a number and increment as you go.

```csharp
using FlyingLogs;

Log.Information.L43("Socket bind success.");
```

When you put the above code in your project, flying-logs detects that you are calling a method under `Log.Information` class. Since it doesn't exist, it will automatically be created. Let's see another example:

```csharp
Log.Error.E78(
    "Throttling player {playerId} after receiving {requestsPerSecond} requests per second on average within the last minute.",
    player.Id,
    totalRequests / 60);
```

Similar to the example before, flying-logs will detect your method call and create an `E78` method under `FlyingLogs.Log.Error` class. It will parse the template and determine the names of your properties. It will look at the types of the arguments you provided and pick the most efficient Utf8 encoding option for each argument. Log level is already determined to be `Error` since we called this method from `Log.Error` type. All this information will be used to immediately generate the most efficient method body to log your event.

As you can see, logging in flying-logs is very easy. There is no boilerplate code needed; you just spell your message and list your arguments and the library handles the rest. 

## Additional Properties

Not every property that you want to include in a log needs to be a part of the message template. You can add more properties by simply adding more arguments to the function call as below.

```csharp
Log.Information("Simulation of map {mapId} completed",
    map.Id,
    threadId: Thread.CurrentThreadId,
    duration: stopwatch.ElapsedMiliseconds);
```

 In this example, `threadId` and `duration` are not part of the template, but we were still able to include them in our log event.
 
 As you may notice, we explicitly specified the names of the last two parameters above. This is required for the properties that aren't part of the template since flying-logs wouldn't know what to name them otherwise.
 
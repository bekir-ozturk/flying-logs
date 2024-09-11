# flying-logs
A high performance, minimal allocation logging library.

> [!NOTE] 
> The library is still in it's early stages. Some features may change. Many others, you might find missing. If you give it a try, consider leaving a feedback in the discussions page. Thank you!

## Why choose flying-logs?
flying-logs was initially developed with game servers in mind. In such scenarios, the server is expected to process the entire world 10 to 60 times per second depending on the game. Such high frequency leaves as low as ~16 milliseconds to handle all the game logic, network calls, metric emissions, and of course, logging.

Although C# & .NET make it not only possible but also very easy to write high performance code, there is one thing that might put your high frame rate at risk: garbage collector. Depending on how much you allocate, GC can suspend your threads for hundreds of milliseconds, putting you way behind your 16ms frame time goal.

To avoid such spikes, many game developers keep allocations low by preloading their resources at launch and by using object pooling. While these get us far in keeping the allocations low, they won't be sufficient if our logging library keeps allocating strings every frame.

With these concerns in mind, flying-logs was created. It makes no allocations in a significant majority of the cases, all the parsing is done at compile time, and it has a very thin abstraction layer to get your logs to their destination as fast as possible.

## How is high performance achieved?
flying-logs is all about source generators. It is empowered by the fact that most of what is to be logged is already known at compile time. The message template in your log, names of the properties, log levels, event ids and any other string that is known at compile time is pre-encoded to Utf8 and stored in byte arrays. At runtime, the task of the sinks is to just copy these memory sections into the target stream in the correct order. Values of your properties, which are not known at compile time, are efficiently encoded at runtime to a preallocated memory block to be used by sinks. flying-logs utilize the latest `IUtf8SpanFormattable` APIs to encode the most common types without ever calling `ToString()` on them. Any string that needs to be allocated is quickly discarded after use to decrease the chance of it surviving a Gen0 collection by the GC.

## What's the catch?
Depending heavily on 'knowing things at compile-time', flying-logs offers a much smaller feature set than what you might find elsewhere. Some notable limitations are:
- All the output is UTF8 encoded today. If you want to output to an ASCII formatted file, you'll need to do the conversion in your sink.
- No [enrichers](https://github.com/serilog/serilog/wiki/Enrichment) (although, they are on the way.).
- No [scopes](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0#log-scopes).
- No support for [expanding collections](https://github.com/serilog/serilog/wiki/Structured-Data#collections).
- No support for expanding anonymous types or types with restricted visilibility (private/protected).

You can find a bigger list of limitations in the [Limitations](/docs/Limitations.md) page along with some useful workarounds.

## Getting Started

To get started, you need the source generator package in your project:

```
dotnet add package FlyingLogs
```

With this package alone we are ready to log messages, but they won't show up anywhere until we configure some sinks. Console sink is already included in the `FlyingLogs` package. If you want the `Seq` sink, you can add `FlyingLogs.Sinks.Seq` package respectively. There is no file sink yet (tracked in https://github.com/bekir-ozturk/flying-logs/issues/7).

```csharp
FlyingLogs.Configuration.Initialize(new ConsoleSink());
```

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

Using [message templates]() , structured logging was built into flying-logs from day one. Instead of converting your log event to a simple string, flying-logs will keep your properties and their names separate, allowing you to run queries over your logs.

As you can see, logging in flying-logs is very easy. There is no boilerplate code needed; you just spell your message and list your arguments and the library handles the rest. 

## Additional Properties

Not every property that you want to include in a log needs to be a part of the message template. You can add more properties by simply adding more arguments to the function call as below.

```csharp
Log.Information("Simulation of map {mapId} completed.",
    map.Id,
    threadId: Thread.CurrentThreadId,
    duration: stopwatch.ElapsedMiliseconds);
```

 In this example, `threadId` and `duration` are not part of the template, but we were still able to include them in our log event.
 
 As you may notice, we explicitly specified the names of the last two parameters above. This is required for the properties that aren't part of the template since flying-logs wouldn't know what to name them otherwise.
 
 ## Expanding complex objects into primitive fields

Sometimes the property you want to include in a log event will not be a simple `int` or `string` but a larger type with multiple important fields or properties. For instance, a `playerPosition` property of type `Vector3` which contains 3 floats: `X`, `Y` and `Z`.

```csharp
public struct Vector3
{
    public float X;
    public float Y;
    public float Z;

    public override string ToString() => X + ", " + Y + ", " + Z;
}
```

One way to log this event would be to do this:

```csharp
Log.Error.L5("Invalid position {position}", playerPosition);
```

However this will internally convert the property to its string representation.

```
position: "1028.4, 48.9, 637"
```

This means that you won't be able to filter these messages based on the value of X, Y or Z individually.

Instead, you can tell flying-logs to 'expand' this struct into it's fields by marking the property with `@`.

```csharp
Log.Error.L5("Invalid position {@position}", playerPosition);
```

This will allow each property to be stored in the logs individually and allow filtering later on.

```
position: { X:1028.4, Y:48.9, Z:637 }
```

You can also expand additional properties that aren't part of your message template. The `@` sign would need to come just before the parameter name as it was with the named properties.

```csharp
Log.Error.L5("Unexpected fall damage.", @position: new Vector3 (1028.4f, 48.9f, 637f))
```
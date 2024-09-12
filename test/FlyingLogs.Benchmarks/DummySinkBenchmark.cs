using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using FlyingLogs.Benchmarks;
using FlyingLogs.Core.Sinks;

using NLog;
using Serilog;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class DummySinkBenchmarks
{
    private Serilog.Core.Logger? _logger;
    private NLog.Logger _nlogLogger;


    private const int _bookCount = 1_000;
    private ulong _bookIndex = 0;
    private Book[] _books;
    private BookStruct[] _bookStructs;

    [Params("DISABLED", "NOOPSINK", "CLEF")]
    public string SinkConfig;

    private void InitializeBooksArray()
    {
        var random = new Random();
        _books = new Book[_bookCount];
        for(int i=0; i<_bookCount; i++)
        {
            var book = new Book()
            {
                Id = random.Next(),
                Isbn = random.NextInt64().ToString(),
                Publisher = new Publisher()
                    {
                        Address = new Address()
                            {
                                StreetName = random.NextInt64().ToString(),
                                CountryOrRegion = (CountryOrRegion)random.Next(0, 4)
                            }
                    }
            };
            _books[i] = book;
        }
    }

    private void InitializeBookStructsArray()
    {
        var random = new Random();
        _bookStructs = new BookStruct[_bookCount];
        for (int i = 0; i < _bookCount; i++)
        {
            var book = new BookStruct()
            {
                Id = random.Next(),
                Isbn = random.NextInt64().ToString(),
                Publisher = new PublisherStruct()
                    {
                        Address = new AddressStruct()
                            {
                                StreetName = random.NextInt64().ToString(),
                                CountryOrRegion = (CountryOrRegion)random.Next(0, 4)
                            }
                    }
            };
            _bookStructs[i] = book;
        }
    }


    [GlobalSetup]
    public void Setup()
    {
        InitializeBooksArray();
        InitializeBookStructsArray();

        switch (SinkConfig)
        {
            case "DISABLED":
                GlobalSetupDisabled();
                break;
            case "NOOPSINK":
                GlobalSetupNoOpSink();
                break;
            case "CLEF":
                GlobalSetupClef();
                break;
            default:
                throw new Exception("Unexpected env var BENCHMARK_SETUP");
        }
    }

    private void GlobalSetupDisabled()
    {
        FlyingLogs.Configuration.Initialize(
            new ClefFormatter(new DummyFlyingLogsClefSink(FlyingLogs.Shared.LogLevel.Critical)));

        _logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Fatal()
            .WriteTo.Sink(new DummySerilogClefSink())
            .CreateLogger();

        NLog.LogManager.Setup().LoadConfiguration(builder => {
            builder.ForLogger().FilterMinLevel(LogLevel.Fatal).Targets.Add(new DummyNlogSink());
        });
        _nlogLogger = NLog.LogManager.GetCurrentClassLogger();
    }

    private void GlobalSetupNoOpSink()
    {
        FlyingLogs.Configuration.Initialize(new DummyFlyingLogsSink());

        _logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new DummySerilogSink())
            .CreateLogger();

        NLog.LogManager.Setup().LoadConfiguration(builder => {
            builder.ForLogger().FilterMinLevel(LogLevel.Trace).Targets.Add(new DummyNlogSink());
        });
        _nlogLogger = NLog.LogManager.GetCurrentClassLogger();
    }

    private void GlobalSetupClef()
    {
        FlyingLogs.Configuration.Initialize(
            new ClefFormatter(new DummyFlyingLogsClefSink()));

        _logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new DummySerilogClefSink())
            .CreateLogger();

        // NlogClefTarget needs to be copied from official Nlog repository along with any dependencies.
        /*NLog.LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(LogLevel.Trace).Targets.Add(new NLog.Targets.Seq.NlogClefTarget());
        });*/
        _nlogLogger = NLog.LogManager.GetCurrentClassLogger();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Simple")]
    public void SerilogSimple()
    {
        _logger!.Error("This is an error.");
    }

    [Benchmark, BenchmarkCategory("Simple")]
    public void FlyingLogsSimple()
    {
        FlyingLogs.Log.Error.L1("This is an error.");
    }

    [Benchmark, BenchmarkCategory("Simple")]
    public void NlogSimple()
    {
        _nlogLogger.Error("This is an error.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("OneInt")]
    public void SerilogOneInt()
    {
        _logger!.Error("This is just {count} more error.", 1);
    }

    [Benchmark, BenchmarkCategory("OneInt")]
    public void FlyingLogsOneInt()
    {
        FlyingLogs.Log.Error.L2("This is just {count} more error.", 1);
    }

    [Benchmark, BenchmarkCategory("OneInt")]
    public void NlogOneInt()
    {
        _nlogLogger.Error("This is just {count} more error.", 1);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("OneEnum")]
    public void SerilogOneEnum()
    {
        _logger!.Error("Publisher found in {location}.", CountryOrRegion.Italy);
    }

    [Benchmark, BenchmarkCategory("OneEnum")]
    public void FlyingLogsOneEnum()
    {
        FlyingLogs.Log.Error.L7("Publisher found in {location}.", CountryOrRegion.Italy);
    }

    [Benchmark, BenchmarkCategory("OneEnum")]
    public void NlogOneEnum()
    {
        _nlogLogger.Error("Publisher found in {location}.", CountryOrRegion.Italy);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("OneBook")]
    public void SerilogOneBook()
    {
        _logger!.Error("You should read this {book} I bought.", _books[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBook")]
    public void FlyingLogsOneBook()
    {
        FlyingLogs.Log.Error.L3("You should read this {book} I bought.", _books[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBook")]
    public void NlogOneBook()
    {
        _nlogLogger.Error("You should read this {book} I bought.", _books[_bookIndex++ % _bookCount]);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("OneBookExpanded")]
    public void SerilogOneBookExpanded()
    {
        _logger!.Error("You should read this {@book} I bought.", _books[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBookExpanded")]
    public void FlyingLogsOneBookExpanded()
    {
        FlyingLogs.Log.Error.L5("You should read this {@book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBookExpanded")]
    public void NogOneBookExpanded()
    {
        _nlogLogger.Error("You should read this {@book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBook")]
    public void SerilogOneBookStruct()
    {
        _logger!.Error("You should read this {book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBook")]
    public void FlyingLogsOneBookStruct()
    {
        FlyingLogs.Log.Error.L6("You should read this {book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBook")]
    public void NlogOneBookStruct()
    {
        _nlogLogger.Error("You should read this {book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBookExpanded")]
    public void SerilogOneBookStructExpanded()
    {
        _logger!.Error("You should read this {@book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBookExpanded")]
    public void FlyingLogsOneBookStructExpanded()
    {
        FlyingLogs.Log.Error.L4("You should read this {@book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }

    [Benchmark, BenchmarkCategory("OneBookExpanded")]
    public void NlogOneBookStructExpanded()
    {
        _nlogLogger.Error("You should read this {@book} I bought.", _bookStructs[_bookIndex++ % _bookCount]);
    }
}
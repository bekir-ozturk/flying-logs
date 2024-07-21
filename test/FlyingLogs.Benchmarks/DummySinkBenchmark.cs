using BenchmarkDotNet.Attributes;

using FlyingLogs.Core.Sinks;

[MemoryDiagnoser]
public class FlyingLogsVsSerilog
{
    private Serilog.Core.Logger? _logger;
    
    private const int _bookCount = 1_000;
    private Book[] _books;

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
                Publisher = random.Next(0,100) < 40 ? null :
                    new Publisher()
                    {
                        Address = random.Next(0, 100) < 50 ? null : 
                            new Address()
                            {
                                StreetName = random.NextInt64().ToString(),
                                CountryOrRegion = (CountryOrRegion)random.Next(0, 4)
                            }
                    }
            };
            _books[i] = book;
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        InitializeBooksArray();
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
    }

    private void GlobalSetupNoOpSink()
    {
        FlyingLogs.Configuration.Initialize(new DummyFlyingLogsSink());

        _logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new DummySerilogSink())
            .CreateLogger();
    }

    private void GlobalSetupClef()
    {
        FlyingLogs.Configuration.Initialize(
            new ClefFormatter(new DummyFlyingLogsClefSink()));

        _logger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new DummySerilogClefSink())
            .CreateLogger();
    }

    [Benchmark]
    public void SerilogSimple()
    {
        _logger!.Error("This is an error.");
    }

    [Benchmark]
    public void FlyingLogsSimple()
    {
        FlyingLogs.Log.Error.L1("This is an error.");
    }

    [Benchmark]
    public void SerilogOneInt()
    {
        _logger!.Error("This is just {count} more error.", 1);
    }

    [Benchmark]
    public void FlyingLogsOneInt()
    {
        FlyingLogs.Log.Error.L2("This is just {count} more error.", 1);
    }

    [Benchmark]
    public void SerilogOneBook()
    {
        for (int i=0; i< _bookCount; i++)
        {
            _logger!.Error("You should read this {book} I bought.", _books[i]);
        }
    }

    [Benchmark]
    public void FlyingLogsOneBook()
    {
        for (int i=0; i< _bookCount; i++)
        {
            FlyingLogs.Log.Error.L3("You should read this {book} I bought.", _books[i]);
        }
    }

    [Benchmark]
    public void SerilogOneBookExpanded()
    {
        for (int i=0; i< _bookCount; i++)
        {
            _logger!.Error("You should read this {@book} I bought.", _books[i]);
        }
    }

    [Benchmark]
    public void FlyingLogsOneBookExpanded()
    {
        for (int i=0; i< _bookCount; i++)
        {
            FlyingLogs.Log.Error.L4("You should read this {@book} I bought.", _books[i]);
        }
    }
}
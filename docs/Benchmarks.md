# Benchmarks

The following are the results of running the `test/FlyingLogs.Benchmarks/` project with `dotnet run -c Release`.

Below are the cases that we benchmark:
```
// Simple
_logger.Error("This is an error.");

// OneInt
_logger.Error("This is just {count} more error.", 1);

// OneEnum
_logger.Error("Publisher found in {location}.", CountryOrRegion.Italy);

// OneBook
_logger.Error("You should read this {book} I bought.", book);

// OneBookExpanded
_logger.Error("You should read this {@book} I bought.", book);
```

The `Book` type implementation can be found [here](https://github.com/bekir-ozturk/flying-logs/blob/c9714c5bf1a62f32fcca2c424abb040f17d173ba/test/FlyingLogs.Benchmarks/Models.cs).

You may notice that the flying-logs is allocating memory for OneBook and OneBookExpanded cases.
The reasons behind are as follows:
- OneBook: the type `Book` doesn't implement `IUtf8SpanFormattable` so the only way for us to convert it to a string representation is to call `ToString()` method. This results in a string allocation.
  - This can be avoided by simply implementing `IUtf8SpanFormattable` on the type.
  - I chose to not implement this in the benchmarks to keep the competition somewhat fair.
- OneBookExpanded: the type `Book`, when expanded, exposes the enum field under `book.Publisher.Address.CountryOrRegion`.
Similar to the case above, enums don't implement `IUtf8SpanFormattable` and therefore cause allocations when `ToString()` is called on them.
  - See https://github.com/bekir-ozturk/flying-logs/issues/3 for workarounds.

## Scenario 1 - Log events are passed to a sink that just discards them 

The sinks in this scenario acquire the log event details from the logging library, but simply discard the inputs and return immediately without writing the output anywhere.

| Method                          | Mean          | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |--------------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|
| SerilogSimple                   |   310.6807 ns |   6.2329 ns |   8.1045 ns |  1.00 |    0.00 | 0.0381 |     160 B |        1.00 |
| FlyingLogsSimple                |    38.9521 ns |   0.7946 ns |   0.8832 ns |  0.12 |    0.00 |      - |         - |        0.00 |
| NlogSimple                      |    78.3286 ns |   1.5862 ns |   2.4696 ns |  0.25 |    0.01 | 0.0286 |     120 B |        0.75 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneInt                   |   442.1392 ns |   8.7766 ns |  14.4202 ns |  1.00 |    0.00 | 0.0916 |     384 B |        1.00 |
| FlyingLogsOneInt                |    53.3609 ns |   1.0764 ns |   1.6111 ns |  0.12 |    0.01 |      - |         - |        0.00 |
| NlogOneInt                      |   100.9731 ns |   2.0129 ns |   2.9505 ns |  0.23 |    0.01 | 0.0421 |     176 B |        0.46 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneEnum                  |   481.9796 ns |   9.1342 ns |  10.1526 ns |  1.00 |    0.00 | 0.0916 |     384 B |        1.00 |
| FlyingLogsOneEnum               |    83.4271 ns |   1.6934 ns |   2.9658 ns |  0.17 |    0.01 | 0.0057 |      24 B |        0.06 |
| NlogOneEnum                     |    98.1033 ns |   1.9848 ns |   3.2611 ns |  0.20 |    0.01 | 0.0421 |     176 B |        0.46 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneBook                  |   469.0421 ns |   7.7582 ns |  10.8760 ns |  1.00 |    0.00 | 0.0858 |     360 B |        1.00 |
| FlyingLogsOneBook               |    70.7959 ns |   1.0564 ns |   0.8248 ns |  0.15 |    0.00 |      - |         - |        0.00 |
| NlogOneBook                     |   616.5388 ns |  11.9467 ns |  16.7477 ns |  1.32 |    0.05 | 0.0916 |     384 B |        1.07 |
| SerilogOneBookStruct            |   579.5358 ns |  10.7373 ns |  14.3340 ns |  1.24 |    0.05 | 0.1011 |     424 B |        1.18 |
| FlyingLogsOneBookStruct         |   143.1464 ns |   1.6616 ns |   1.2972 ns |  0.30 |    0.01 | 0.0153 |      64 B |        0.18 |
| NlogOneBookStruct               |   614.3037 ns |  12.0934 ns |  13.4418 ns |  1.31 |    0.04 | 0.1087 |     456 B |        1.27 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneBookExpanded          | 3,631.4916 ns |  70.3464 ns |  88.9657 ns |  1.00 |    0.00 | 0.5798 |    2440 B |       1.000 |
| FlyingLogsOneBookExpanded       |   300.7729 ns |   5.9724 ns |   7.9730 ns |  0.08 |    0.00 | 0.0057 |      24 B |       0.010 |
| NogOneBookExpanded              | 2,183.3159 ns |  43.4213 ns |  73.7326 ns |  0.60 |    0.02 | 0.2632 |    1110 B |       0.455 |
| SerilogOneBookStructExpanded    | 3,865.8659 ns |  69.9850 ns | 118.8398 ns |  1.06 |    0.05 | 0.6142 |    2576 B |       1.056 |
| FlyingLogsOneBookStructExpanded |   305.6694 ns |   6.0533 ns |  10.9153 ns |  0.08 |    0.00 | 0.0057 |      24 B |       0.010 |
| NlogOneBookStructExpanded       | 2,163.4105 ns |  41.0800 ns |  45.6603 ns |  0.59 |    0.02 | 0.2632 |    1110 B |       0.455 |

## Scenario 2 - Log events are passed to a sink that constructs a CLEF string

The sinks in this configuration acquire the log event details from the logging library, then construct a CLEF formatted Utf8 string out of it. The result is then discarded without being written anywhere.

The only difference between this and the previous scenario is the construction of the CLEF string.
But the performance of the CLEF formatter is not the only thing we are benchmarking here.
We also take into account the cost of converting the data provided by the library into something that the formatter can work with.

In scenario 1, the sinks received some data, but didn't do anything with it.
We don't even know if the logging libraries did anything; they may have some lazy implementation that doesn't touch the data until it is needed.
In fact, Serilog implementation does just that. The value of a property is not converted to a string until someone asks for it.

This laziness presents itself as a cost when you actually need the data.
Therefore, the construction of a CLEF string is helpful here to expose the hidden costs.

| Method                          | Mean          | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |--------------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|
| SerilogSimple                   |   932.5617 ns |  18.5871 ns |  29.4812 ns |  1.00 |    0.00 | 0.0973 |     408 B |        1.00 |
| FlyingLogsSimple                |   221.9931 ns |   3.8364 ns |   3.7679 ns |  0.24 |    0.01 |      - |         - |        0.00 |
| NlogSimple                      | 1,560.4988 ns |  30.3658 ns |  28.4042 ns |  1.68 |    0.05 | 0.2308 |     968 B |        2.37 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneInt                   | 1,363.6377 ns |  26.9559 ns |  57.4453 ns |  1.00 |    0.00 | 0.1640 |     688 B |        1.00 |
| FlyingLogsOneInt                |   308.4286 ns |   5.9477 ns |   8.3379 ns |  0.23 |    0.01 |      - |         - |        0.00 |
| NlogOneInt                      | 2,490.5265 ns |  49.4506 ns |  89.1696 ns |  1.83 |    0.09 | 0.3014 |    1272 B |        1.85 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneEnum                  | 1,384.6875 ns |  27.3695 ns |  32.5814 ns |  1.00 |    0.00 | 0.1640 |     688 B |        1.00 |
| FlyingLogsOneEnum               |   429.7792 ns |   8.3983 ns |  11.7733 ns |  0.31 |    0.01 | 0.0057 |      24 B |        0.03 |
| NlogOneEnum                     | 2,885.5202 ns |  56.5101 ns | 111.5454 ns |  2.12 |    0.09 | 0.3052 |    1280 B |        1.86 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneBook                  | 1,283.1515 ns |  24.9561 ns |  26.7027 ns |  1.00 |    0.00 | 0.1583 |     664 B |        1.00 |
| FlyingLogsOneBook               |   344.4209 ns |   1.4865 ns |   1.2413 ns |  0.27 |    0.01 |      - |         - |        0.00 |
| NlogOneBook                     | 2,884.2858 ns |  16.0283 ns |  12.5138 ns |  2.25 |    0.05 | 0.3967 |    1672 B |        2.52 |
| SerilogOneBookStruct            | 1,281.7565 ns |   7.6856 ns |   7.1891 ns |  1.00 |    0.02 | 0.1736 |     728 B |        1.10 |
| FlyingLogsOneBookStruct         |   411.2004 ns |   1.8326 ns |   1.7142 ns |  0.32 |    0.01 | 0.0153 |      64 B |        0.10 |
| NlogOneBookStruct               | 3,292.7421 ns |  29.5965 ns |  27.6846 ns |  2.57 |    0.05 | 0.4234 |    1784 B |        2.69 |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneBookExpanded          | 5,759.2112 ns | 114.4852 ns | 101.4881 ns |  1.00 |    0.00 | 0.6866 |    2889 B |       1.000 |
| FlyingLogsOneBookExpanded       | 1,247.8671 ns |  24.8903 ns |  53.0432 ns |  0.21 |    0.01 | 0.0057 |      24 B |       0.008 |
| NogOneBookExpanded              | 5,970.9933 ns | 115.4467 ns | 102.3405 ns |  1.04 |    0.02 | 0.6332 |    2670 B |       0.924 |
| SerilogOneBookStructExpanded    | 6,743.6308 ns | 123.1041 ns | 221.9821 ns |  1.20 |    0.03 | 0.7172 |    3025 B |       1.047 |
| FlyingLogsOneBookStructExpanded | 1,232.7450 ns |  24.2453 ns |  44.3340 ns |  0.21 |    0.01 | 0.0057 |      24 B |       0.008 |
| NlogOneBookStructExpanded       | 6,114.5624 ns | 119.6394 ns | 167.7177 ns |  1.07 |    0.03 | 0.6332 |    2670 B |       0.924 |

## Scenario 3 - Log level are turned off
In this scenario, sinks were configured to only listen to `Fatal` or `Critical` log events, but none of the events in the benchmarks had such high severity.

This is the scenario where flying-logs performs worse compared to others (for now).
This is simply because the log methods created by flying-logs are larger in size and don't get inlined.
Where others perform a simple 'level check' and skip the log method entirely,
flying-logs methods suffer the performance penalty of copying all the arguments and performing the function call.

These calls are still very fast and shouldn't matter in almost any real-world case. But a fix (https://github.com/bekir-ozturk/flying-logs/issues/8) to allow inlining log level checks is also planned.

| Method                          | Mean          | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |--------------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|
| SerilogSimple                   |     4.3017 ns |   0.1195 ns |   0.1596 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsSimple                |     1.1875 ns |   0.0571 ns |   0.0534 ns |  0.28 |    0.02 |      - |         - |          NA |
| NlogSimple                      |     1.0131 ns |   0.0549 ns |   0.0714 ns |  0.24 |    0.02 |      - |         - |          NA |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneInt                   |     0.6725 ns |   0.0493 ns |   0.0642 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneInt                |     3.5353 ns |   0.1069 ns |   0.1695 ns |  5.26 |    0.52 |      - |         - |          NA |
| NlogOneInt                      |     1.2497 ns |   0.0602 ns |   0.0618 ns |  1.82 |    0.18 |      - |         - |          NA |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneEnum                  |     0.6261 ns |   0.0464 ns |   0.0553 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneEnum               |     3.0984 ns |   0.0976 ns |   0.1335 ns |  4.96 |    0.50 |      - |         - |          NA |
| NlogOneEnum                     |     1.1153 ns |   0.0565 ns |   0.0694 ns |  1.79 |    0.21 |      - |         - |          NA |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneBook                  |     1.6222 ns |   0.0099 ns |   0.0092 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneBook               |     3.7080 ns |   0.0224 ns |   0.0210 ns |  2.29 |    0.02 |      - |         - |          NA |
| NlogOneBook                     |     2.0764 ns |   0.0105 ns |   0.0088 ns |  1.28 |    0.01 |      - |         - |          NA |
| SerilogOneBookStruct            |     4.5108 ns |   0.3814 ns |   1.1245 ns |  2.77 |    1.06 |      - |         - |          NA |
| FlyingLogsOneBookStruct         |     5.9145 ns |   0.1539 ns |   0.3443 ns |  3.91 |    0.19 |      - |         - |          NA |
| NlogOneBookStruct               |     3.0511 ns |   0.0837 ns |   0.1059 ns |  1.87 |    0.07 |      - |         - |          NA |
|                                 |               |             |             |       |         |        |           |             |
| SerilogOneBookExpanded          |     1.9041 ns |   0.0705 ns |   0.1139 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneBookExpanded       |    10.8812 ns |   0.2527 ns |   0.4360 ns |  5.74 |    0.36 |      - |         - |          NA |
| NogOneBookExpanded              |     3.1697 ns |   0.0931 ns |   0.1606 ns |  1.67 |    0.14 |      - |         - |          NA |
| SerilogOneBookStructExpanded    |     3.3656 ns |   0.0985 ns |   0.0921 ns |  1.83 |    0.13 |      - |         - |          NA |
| FlyingLogsOneBookStructExpanded |    10.8261 ns |   0.2440 ns |   0.2611 ns |  5.86 |    0.36 |      - |         - |          NA |
| NlogOneBookStructExpanded       |     3.2270 ns |   0.0875 ns |   0.1665 ns |  1.71 |    0.11 |      - |         - |          NA |

## Scenario 4 - Testing end-to-end with a real sink

Well... I don't have the results for this. Benchmarking turned out much harder than I anticipated when network-bound asynchronous background workers are involved (specifically, the Seq sink).
Some notes:

- It is common that the sink batches a few log events and sends them in a single request. Micro-benchmarking isn't possible.
- Logging a batch of events and recording the time isn't useful since the data isn't always flushed right away. Sinks need to be drained to ensure all the events are processed.
Draining is only expected to be called once during the lifetime of the application and therefore may not be optimized well. Including it in the benchmarks will very likely skew the results.
- Sending 10k log events in a tight loop is not a realistic scenario for the most applications. Logs are commonly sprinkled in the code where the threads do other work in-between log calls.
If multiple threads are logging events simultaneously, there is much less lock contention to happen in a real life scenario compared to a benchmark.
It is reasonable to expect sinks to be optimized for the common scenario and therefore it wouldn't be fair to evaluate their performance in tight loops with high locks contention.

It should be possible to run some end-to-end benchmarks with synchronous sinks. Console sink, for instance, directly pushes the result to output stream - no asynchrony involved.
However, it is very likely that the time spent on printing to console will dominate the results and it won't be possible to compare the performance of the libraries.
Moreover, console sink being mostly for debugging/development scenarios, it is fair to expect that it wouldn't be the most optimized code of the library.

We don't have a file sink today, but it may also be a valid candidate for such testing once implemented.
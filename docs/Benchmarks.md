# Benchmarks

The following are the results of running the `test/FlyingLogs.Benchmarks/` project with `dotnet run -c Release`.

SinkConfig values mean:
  - DISABLED: Sinks were configured to only listen to `Fatal` or `Critical` log events, but none of the events in the benchmarks had such high severity.
  - NOOPSINK: Sinks in this configuration acquire the log event details from the logging library, but simply discard the inputs and return immediately without writing the output anywhere.
  - CLEF    : Sinks in this configuration acquire the log event details from the logging library, then construct a Clef formatted Utf8 string out of it. The result is then discarded without being written anywhere.

| Method                          | Categories      | SinkConfig | Mean          | Error       | StdDev      | Median        | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |---------------- |----------- |--------------:|------------:|------------:|--------------:|------:|--------:|-------:|----------:|------------:|
| SerilogSimple                   | Simple          | DISABLED   |     3.5801 ns |   0.0343 ns |   0.0304 ns |     3.5803 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsSimple                | Simple          | DISABLED   |     1.0223 ns |   0.0117 ns |   0.0109 ns |     1.0208 ns |  0.29 |    0.00 |      - |         - |          NA |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneInt                   | OneInt          | DISABLED   |     0.6222 ns |   0.0449 ns |   0.0699 ns |     0.6083 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneInt                | OneInt          | DISABLED   |     2.3646 ns |   0.0969 ns |   0.1565 ns |     2.4430 ns |  3.83 |    0.47 |      - |         - |          NA |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneEnum                  | OneEnum         | DISABLED   |     0.5185 ns |   0.0080 ns |   0.0067 ns |     0.5209 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneEnum               | OneEnum         | DISABLED   |     2.6653 ns |   0.0158 ns |   0.0148 ns |     2.6619 ns |  5.14 |    0.08 |      - |         - |          NA |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneBook                  | OneBook         | DISABLED   |     1.6226 ns |   0.0127 ns |   0.0113 ns |     1.6188 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneBook               | OneBook         | DISABLED   |     3.7234 ns |   0.0164 ns |   0.0137 ns |     3.7206 ns |  2.29 |    0.02 |      - |         - |          NA |
| SerilogOneBookStruct            | OneBook         | DISABLED   |     2.8055 ns |   0.0188 ns |   0.0176 ns |     2.8044 ns |  1.73 |    0.02 |      - |         - |          NA |
| FlyingLogsOneBookStruct         | OneBook         | DISABLED   |     5.3659 ns |   0.0439 ns |   0.0410 ns |     5.3593 ns |  3.31 |    0.04 |      - |         - |          NA |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneBookExpanded          | OneBookExpanded | DISABLED   |     1.7359 ns |   0.0707 ns |   0.1080 ns |     1.7873 ns |  1.00 |    0.00 |      - |         - |          NA |
| FlyingLogsOneBookExpanded       | OneBookExpanded | DISABLED   |    10.1854 ns |   0.1105 ns |   0.1033 ns |    10.2030 ns |  6.09 |    0.42 |      - |         - |          NA |
| SerilogOneBookStructExpanded    | OneBookExpanded | DISABLED   |     2.9229 ns |   0.0943 ns |   0.1383 ns |     2.9542 ns |  1.69 |    0.11 |      - |         - |          NA |
| FlyingLogsOneBookStructExpanded | OneBookExpanded | DISABLED   |     9.9070 ns |   0.2330 ns |   0.3341 ns |    10.0326 ns |  5.75 |    0.28 |      - |         - |          NA |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogSimple                   | Simple          | NOOPSINK   |   285.1763 ns |   1.1698 ns |   1.0370 ns |   285.3026 ns |  1.00 |    0.00 | 0.0381 |     160 B |        1.00 |
| FlyingLogsSimple                | Simple          | NOOPSINK   |    28.9889 ns |   0.1112 ns |   0.0928 ns |    28.9893 ns |  0.10 |    0.00 |      - |         - |        0.00 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneInt                   | OneInt          | NOOPSINK   |   419.8653 ns |   8.3881 ns |  13.0593 ns |   424.1617 ns |  1.00 |    0.00 | 0.0916 |     384 B |        1.00 |
| FlyingLogsOneInt                | OneInt          | NOOPSINK   |    53.0510 ns |   1.0924 ns |   1.3415 ns |    53.7657 ns |  0.13 |    0.00 |      - |         - |        0.00 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneEnum                  | OneEnum         | NOOPSINK   |   462.1407 ns |   5.5196 ns |   4.6091 ns |   462.0955 ns |  1.00 |    0.00 | 0.0916 |     384 B |        1.00 |
| FlyingLogsOneEnum               | OneEnum         | NOOPSINK   |    83.2829 ns |   0.6195 ns |   0.5795 ns |    83.1833 ns |  0.18 |    0.00 | 0.0057 |      24 B |        0.06 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneBook                  | OneBook         | NOOPSINK   |   480.2063 ns |   7.7978 ns |   7.2941 ns |   476.6867 ns |  1.00 |    0.00 | 0.0858 |     360 B |        1.00 |
| FlyingLogsOneBook               | OneBook         | NOOPSINK   |    61.5112 ns |   0.3359 ns |   0.2978 ns |    61.4453 ns |  0.13 |    0.00 |      - |         - |        0.00 |
| SerilogOneBookStruct            | OneBook         | NOOPSINK   |   605.3612 ns |  12.0367 ns |  25.6511 ns |   602.7079 ns |  1.26 |    0.07 | 0.1011 |     424 B |        1.18 |
| FlyingLogsOneBookStruct         | OneBook         | NOOPSINK   |   141.1321 ns |   2.8277 ns |   5.2413 ns |   142.8385 ns |  0.29 |    0.01 | 0.0153 |      64 B |        0.18 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneBookExpanded          | OneBookExpanded | NOOPSINK   | 3,134.9784 ns |  55.3508 ns |  51.7752 ns | 3,157.5497 ns |  1.00 |    0.00 | 0.5798 |    2440 B |       1.000 |
| FlyingLogsOneBookExpanded       | OneBookExpanded | NOOPSINK   |   271.5336 ns |   0.9482 ns |   0.8406 ns |   271.2530 ns |  0.09 |    0.00 | 0.0057 |      24 B |       0.010 |
| SerilogOneBookStructExpanded    | OneBookExpanded | NOOPSINK   | 3,262.6233 ns |  16.8635 ns |  14.9491 ns | 3,257.5504 ns |  1.04 |    0.02 | 0.6142 |    2576 B |       1.056 |
| FlyingLogsOneBookStructExpanded | OneBookExpanded | NOOPSINK   |   264.1153 ns |   0.9912 ns |   0.9272 ns |   263.9897 ns |  0.08 |    0.00 | 0.0057 |      24 B |       0.010 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogSimple                   | Simple          | CLEF       |   882.1571 ns |  17.6102 ns |  36.3682 ns |   886.8958 ns |  1.00 |    0.00 | 0.0973 |     408 B |        1.00 |
| FlyingLogsSimple                | Simple          | CLEF       |   209.2050 ns |   4.1516 ns |   6.8212 ns |   211.3621 ns |  0.24 |    0.01 |      - |         - |        0.00 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneInt                   | OneInt          | CLEF       | 1,140.6478 ns |  10.5442 ns |   9.3471 ns | 1,139.2865 ns |  1.00 |    0.00 | 0.1640 |     688 B |        1.00 |
| FlyingLogsOneInt                | OneInt          | CLEF       |   277.2050 ns |   1.1943 ns |   1.0587 ns |   276.9433 ns |  0.24 |    0.00 |      - |         - |        0.00 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneEnum                  | OneEnum         | CLEF       | 1,146.7327 ns |  18.2337 ns |  17.0558 ns | 1,140.8066 ns |  1.00 |    0.00 | 0.1640 |     688 B |        1.00 |
| FlyingLogsOneEnum               | OneEnum         | CLEF       |   350.2340 ns |   3.0569 ns |   2.8595 ns |   350.3680 ns |  0.31 |    0.01 | 0.0057 |      24 B |        0.03 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneBook                  | OneBook         | CLEF       | 1,260.1211 ns |  21.1918 ns |  18.7860 ns | 1,257.6068 ns |  1.00 |    0.00 | 0.1583 |     664 B |        1.00 |
| FlyingLogsOneBook               | OneBook         | CLEF       |   351.1418 ns |   1.2695 ns |   1.1253 ns |   351.0052 ns |  0.28 |    0.00 |      - |         - |        0.00 |
| SerilogOneBookStruct            | OneBook         | CLEF       | 1,303.5623 ns |  25.6333 ns |  42.1163 ns | 1,278.8029 ns |  1.04 |    0.04 | 0.1736 |     728 B |        1.10 |
| FlyingLogsOneBookStruct         | OneBook         | CLEF       |   407.8315 ns |   1.7196 ns |   1.5243 ns |   407.8584 ns |  0.32 |    0.00 | 0.0153 |      64 B |        0.10 |
|                                 |                 |            |               |             |             |               |       |         |        |           |             |
| SerilogOneBookExpanded          | OneBookExpanded | CLEF       | 6,103.6676 ns | 119.3076 ns | 208.9576 ns | 6,153.0823 ns |  1.00 |    0.00 | 0.6866 |    2889 B |       1.000 |
| FlyingLogsOneBookExpanded       | OneBookExpanded | CLEF       | 1,203.8066 ns |  23.6355 ns |  31.5527 ns | 1,213.8641 ns |  0.20 |    0.01 | 0.0057 |      24 B |       0.008 |
| SerilogOneBookStructExpanded    | OneBookExpanded | CLEF       | 6,047.3279 ns | 120.8789 ns | 238.6032 ns | 6,115.7112 ns |  0.99 |    0.04 | 0.7172 |    3025 B |       1.047 |
| FlyingLogsOneBookStructExpanded | OneBookExpanded | CLEF       | 1,180.8686 ns |  23.4262 ns |  38.4899 ns | 1,195.3274 ns |  0.19 |    0.01 | 0.0057 |      24 B |       0.008 |

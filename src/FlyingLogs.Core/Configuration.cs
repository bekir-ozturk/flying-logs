using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs
{
    public record Config(
        ImmutableArray<LogEncodings> RequiredEncodingsPerLevel,
        ImmutableArray<(LogLevel minlevelOfInterest, ISink sink)> Sinks);

    public static class Configuration
    {
        private static Config _config = new (
            ImmutableArray.Create(
                LogEncodings.None, // Trace
                LogEncodings.None, // Debug
                LogEncodings.None, // Information
                LogEncodings.None, // Warning
                LogEncodings.None, // Error
                LogEncodings.None  // Critical
                ),
            ImmutableArray.Create<(LogLevel, ISink)>());

        public static Config Current => _config;

        public static void Initialize(params (LogLevel maxLevelOfInterest, ISink sink)[] sinks)
        {
            var immutableSinks = sinks.ToImmutableArray();
            LogEncodings[] requiredEncodingsPerLevel = new LogEncodings[(int)LogLevel.None];

            foreach (var sink in immutableSinks)
            {
                for (LogLevel i = sink.maxLevelOfInterest; i < LogLevel.None; i++)
                {
                    requiredEncodingsPerLevel[(int)i] |= sink.sink.ExpectedEncoding;
                }
            }

            Interlocked.Exchange(
                ref _config,
                new Config(requiredEncodingsPerLevel.ToImmutableArray(), immutableSinks));
        }

        public static void SetMinimumLogLevelForSink(params (ISink sink, LogLevel newLevel)[] newLevels)
        {
            var currentConfig = _config;
            var newSinkLevels = currentConfig.Sinks.ToArray();

            for (int i=0; i<newSinkLevels.Length; i++)
            {
                for (int j=0; j<newLevels.Length; j++)
                {
                    if (newSinkLevels[i].sink == newLevels[j].sink)
                        newSinkLevels[i].minlevelOfInterest = newLevels[j].newLevel;
                }
            }

            var requiredEncodingsPerLevel = new LogEncodings[(int)LogLevel.None];
            foreach (var sink in newSinkLevels)
            {
                for (LogLevel i = sink.minlevelOfInterest; i < LogLevel.None; i++)
                {
                    requiredEncodingsPerLevel[(int)i] |= sink.sink.ExpectedEncoding;
                }
            }

            Interlocked.Exchange(
                ref _config,
                new Config(requiredEncodingsPerLevel.ToImmutableArray(), newSinkLevels.ToImmutableArray()));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ExampleGeneratedMethod(LogLevel level)
        {
            Config config = Configuration.Current;
            LogEncodings utf8PlainTargets = config.RequiredEncodingsPerLevel[(int)level] & ~LogEncodings.Utf8Json;
            LogEncodings utf8JsonTargets = config.RequiredEncodingsPerLevel[(int)level] & LogEncodings.Utf8Json;

            RawLog log = new();
            if (utf8PlainTargets != 0 || utf8JsonTargets != 0)
            {
                // Serialize utf8Plain
            }

            if (utf8PlainTargets != 0)
            {
                PourUtf8PlainIntoSinksAndEncodeAsNeeded(config, log, utf8PlainTargets);
            }

            if (utf8JsonTargets != 0)
            {
                // Reserialize to utf8Json
                PourWithoutReencoding(config, log, LogEncodings.Utf8Json);
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void PourUtf8PlainIntoSinksAndEncodeAsNeeded(Config config, RawLog log, LogEncodings targetEncodings, Memory<byte> tmpBuffer)
        {
            Debug.Assert(log.Encoding == LogEncodings.Utf8Plain);

            // Start with Utf8Plain as we already have it ready.
            LogEncodings? nextEncodingToProcess = LogEncodings.Utf8Plain;
            RawLog? currentLog = log;

            do
            {
                LogEncodings currentEncoding = nextEncodingToProcess.Value;
                nextEncodingToProcess = null;

                for (int i = 0; i < config.Sinks.Length; i++)
                {
                    var sink = config.Sinks[i];
                    if (sink.minlevelOfInterest > log.Level)
                        continue;

                    if (sink.sink.ExpectedEncoding != currentEncoding)
                    {
                        // We can't pour into this sync now, we are dealing with a different encoding.
                        if (nextEncodingToProcess == null && (sink.sink.ExpectedEncoding & targetEncodings) != 0)
                        {
                            // But, we are available to pick this up next.
                            nextEncodingToProcess = sink.sink.ExpectedEncoding;
                        }
                        continue;
                    }

                    if (currentLog == null)
                    {
                        // The assembly that reported this event does not hadn't preencode the data into the encoding
                        // requested by this sink. We need to do the reencoding at runtime.
                        if (currentEncoding == LogEncodings.Utf8Json)
                        {
                            currentLog = ThreadCache.RawLogForReencoding.Value!;
                            int usedBufferBytes = Reencoder.ReencodeUtf8PlainToUtf8Json(
                                log,
                                currentLog,
                                tmpBuffer);

                            // Don't reuse the same memory section.
                            tmpBuffer = tmpBuffer.Slice(0, usedBufferBytes);
                        }
                        else
                        {
                            // TODO emit metric: unsupported reencoding request at runtime
                            continue;
                        }
                    }

                    sink.sink.Ingest(currentLog);
                }

                currentLog = null;

            } while (nextEncodingToProcess != null);

        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void PourWithoutReencoding(Config config, RawLog log, LogEncodings targetEncoding)
        {
            for (int i=0; i<config.Sinks.Length; i++)
            {
                var sink = config.Sinks[i];
                if (sink.sink.ExpectedEncoding == targetEncoding
                    && sink.minlevelOfInterest <= log.Level)
                {
                    sink.sink.Ingest(log);
                }
            }
        }
    }
}

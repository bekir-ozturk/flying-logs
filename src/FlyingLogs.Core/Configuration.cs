using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs
{
    public record Config(
        ImmutableArray<LogEncodings> RequiredEncodingsPerLevel,
        ImmutableArray<(LogLevel minLevelOfInterest, Sink sink)> Sinks);

    public static class Configuration
    {
        private static Config _config = new(
            ImmutableArray.Create(
                LogEncodings.None, // Trace
                LogEncodings.None, // Debug
                LogEncodings.None, // Information
                LogEncodings.None, // Warning
                LogEncodings.None, // Error
                LogEncodings.None  // Critical
                ),
            ImmutableArray.Create<(LogLevel, Sink)>());

        public static Config Current => _config;

        public static void Initialize(params (LogLevel maxLevelOfInterest, Sink sink)[] sinks)
        {
            // TODO do not allow the same sink multiple times.
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

        public static void SetMinimumLogLevelForSink(Sink sink, LogLevel newLevel)
        {
            var currentConfig = _config;
            var newSinks = currentConfig.Sinks;

            for (int i = 0; i < currentConfig.Sinks.Length; i++)
            {
                if (currentConfig.Sinks[i].sink == sink)
                {
                    newSinks = newSinks.SetItem(i, (newLevel, sink));
                    break;
                }
            }

            var requiredEncodingsPerLevel = new LogEncodings[(int)LogLevel.None];
            foreach (var s in newSinks)
            {
                for (LogLevel i = s.minLevelOfInterest; i < LogLevel.None; i++)
                {
                    requiredEncodingsPerLevel[(int)i] |= s.sink.ExpectedEncoding;
                }
            }

            Interlocked.Exchange(
                ref _config,
                new Config(requiredEncodingsPerLevel.ToImmutableArray(), newSinks));
        }

        public static void SetMinimumLogLevelForSink(params (Sink sink, LogLevel newLevel)[] newLevels)
        {
            var currentConfig = _config;
            var newSinkLevels = currentConfig.Sinks.ToArray();

            for (int i = 0; i < newSinkLevels.Length; i++)
            {
                for (int j = 0; j < newLevels.Length; j++)
                {
                    if (newSinkLevels[i].sink == newLevels[j].sink)
                        newSinkLevels[i].minLevelOfInterest = newLevels[j].newLevel;
                }
            }

            var requiredEncodingsPerLevel = new LogEncodings[(int)LogLevel.None];
            foreach (var sink in newSinkLevels)
            {
                for (LogLevel i = sink.minLevelOfInterest; i < LogLevel.None; i++)
                {
                    requiredEncodingsPerLevel[(int)i] |= sink.sink.ExpectedEncoding;
                }
            }

            Interlocked.Exchange(
                ref _config,
                new Config(requiredEncodingsPerLevel.ToImmutableArray(), newSinkLevels.ToImmutableArray()));
        }

        /// <summary>
        /// Given a UTF8Plain encoded log, pours it into all the sinks that expect logs with encodings specified in the
        /// <see cref="targetEncodings"/> parameter. For sinks that are already expecting UTF8Plain logs, this means
        /// no reencoding will be needed. However, if some of the target sinks expect a different encoding, then the
        /// original log will be reencoded. Since reencoding will be done on runtime instead of compile time, this
        /// should be avoided as much as possible. But compile time encodings aren't always possible (when consuming a
        /// third party library that was already compiled with the encoding missing, for instance). Currently, we only
        /// support reencoding from UTF8Plain to UTF8Json. Any sink that expects some other encoding will be skipped
        /// instead and a <see cref="Metrics.UnsupportedRuntimeEncoding"/> metric will be emitted.
        /// </summary>
        /// <param name="config">The configuration of FlyingLogs at the time the log method was called. This parameter
        /// should be used instead of <see cref="FlyingLogs.Configuration.Current"/> since the current configuration
        /// could be updated by another thread while we are halfway through processing this log event.</param>
        /// <param name="log">Details of the event that is being logged.</param>
        /// <param name="targetEncodings">Encodings of the sinks that we want to pour this log into. If a sink uses a
        /// different encoding, we won't attempt to pour into it.</param>
        /// <param name="tmpBuffer">Preallocated memory that we can use to add or update the fields of <see cref="log"/>
        /// object. </param>
        /// <returns>The number of bytes used from the tmpBuffer.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static int PourUtf8PlainIntoSinksAndEncodeAsNeeded(Config config, RawLog log, LogEncodings targetEncodings, Memory<byte> tmpBuffer)
        {
            Debug.Assert(log.Encoding == LogEncodings.Utf8Plain);

            LogEncodings? nextEncodingToProcess = LogEncodings.Utf8Plain;
            LogEncodings processedEncodings = LogEncodings.None;
            RawLog? currentLog = log;
            int totalUsedBufferBytes = 0;

            do
            {
                LogEncodings currentEncoding = nextEncodingToProcess.Value;
                nextEncodingToProcess = null;

                for (int i = 0; i < config.Sinks.Length; i++)
                {
                    (var minLevelOfInterest, var sink) = config.Sinks[i];
                    var sinkEncoding = sink.ExpectedEncoding;

                    if ((sinkEncoding & processedEncodings) != 0 || // We already poured into this sink.
                        (sinkEncoding & targetEncodings) == 0) // Or we have no business with it.
                        continue; 

                    if (minLevelOfInterest > log.Level)
                        continue;

                    if ((sinkEncoding & currentEncoding) == 0)
                    {
                        // We can't pour into this sync now, we are dealing with a different encoding.
                        if (nextEncodingToProcess == null)
                        {
                            // But, we are available to pick this up next.
                            nextEncodingToProcess = sinkEncoding;
                        }
                        continue;
                    }

                    if (currentLog == null)
                    {
                        // The assembly that reported this event didn't preencode the data into the encoding
                        // requested by this sink. We need to do the reencoding at runtime.
                        if (currentEncoding == LogEncodings.Utf8Json)
                        {
                            // We know how to reencode from UTF8Plain to UTF8Json.
                            currentLog = ThreadCache.RawLogForReencoding.Value!;
                            int usedBufferBytes = Reencoder.ReencodeUtf8PlainToUtf8Json(
                                log,
                                currentLog,
                                tmpBuffer);
                            totalUsedBufferBytes += usedBufferBytes;

                            // Don't reuse the same memory section.
                            tmpBuffer = tmpBuffer.Slice(usedBufferBytes);
                        }
                        else
                        {
                            // TODO emit metric: unsupported reencoding request at runtime
                            // No need to try the remaining sinks, but we can't break out of the loop just yet.
                            // We need to determine if there is going to be a nextEncodingToProcess.
                            continue;
                        }
                    }

                    sink.Ingest(currentLog);
                }

                processedEncodings |= currentEncoding;
                currentLog = null;

            } while (nextEncodingToProcess != null);

            return totalUsedBufferBytes;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void PourWithoutReencoding(Config config, RawLog log, LogEncodings targetEncoding)
        {
            for (int i = 0; i < config.Sinks.Length; i++)
            {
                (var minLevelOfInterest, var sink) = config.Sinks[i];
                if (sink.ExpectedEncoding == targetEncoding
                    && minLevelOfInterest <= log.Level)
                {
                    sink.Ingest(log);
                }
            }
        }
    }
}

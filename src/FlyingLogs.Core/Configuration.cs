using System.Collections.Immutable;
using System.ComponentModel;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs
{
    public record Config<TSink>(
        LogLevel MinimumLevelOfInterest,
        ImmutableArray<TSink> Sinks) where TSink : ISink
    {
        public Config() : this(
            Configuration.LogLevelNone,
            Sinks: [])
        { }

        public Config(params TSink[] sinks) : this(
            sinks.Length == 0 ? Configuration.LogLevelNone : sinks.Min(s => s.MinimumLevelOfInterest),
            Sinks: [.. sinks])
        { }

        public Config(ImmutableArray<TSink> sinks) : this(
            sinks.Length == 0 ? Configuration.LogLevelNone : sinks.Min(s => s.MinimumLevelOfInterest),
            Sinks: sinks)
        { }
    }

    public static class Configuration
    {
        public const LogLevel LogLevelNone = LogLevel.Critical + 1;

        private static Config<IStructuredUtf8PlainSink> _config = new();

        public static Config<IStructuredUtf8PlainSink> Current => _config;

        public static void Initialize(params IStructuredUtf8PlainSink[] sinks)
        {
            Interlocked.Exchange(ref _config, new Config<IStructuredUtf8PlainSink>(sinks));
        }

        public static void SetMinimumLogLevelForSink(IStructuredUtf8PlainSink sink, LogLevel newLevel)
        {
            ISink.SetLogLevelForSink(ref _config, sink, newLevel);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Pour(
            Config<IStructuredUtf8PlainSink> config,
            LogTemplate logTemplate,
            IReadOnlyList<ReadOnlyMemory<byte>> propertyValues,
            Memory<byte> tmpBuffer)
        {
            for (int i = 0; i < config.Sinks.Length; i++)
            {
                var sink = config.Sinks[i];
                if (sink.MinimumLevelOfInterest <= logTemplate.Level)
                {
                    sink.Ingest(logTemplate, propertyValues, tmpBuffer);
                }
            }
        }
    }
}

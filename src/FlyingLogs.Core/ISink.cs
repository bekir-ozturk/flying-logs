using FlyingLogs.Shared;

namespace FlyingLogs.Core
{
    public interface ISink
    {
        LogLevel MinimumLevelOfInterest { get; }

        internal protected bool SetLogLevelForSink(ISink sink, LogLevel level);

        static internal protected bool SetLogLevelForSink<TSink>(ref Config<TSink> config, ISink sink, LogLevel level) where TSink : ISink
        {
            bool anyChanges = false;
            Config<TSink> configClone = config;
            foreach(var s in configClone.Sinks)
            {
                anyChanges |= s.SetLogLevelForSink(s, level);
            }

            if (anyChanges)
            {
                Interlocked.Exchange(ref config, new (config.Sinks));
            }

            return anyChanges;
        }
    }
}
using System.Collections.Immutable;

using FlyingLogs.Core;

namespace FlyingLogs
{
    public static class Configuration
    {
        private static ImmutableArray<ISink> _activeSinks = ImmutableArray<ISink>.Empty;

        public static ImmutableArray<ISink> ActiveSinks => _activeSinks;

        public static void Initialize(params ISink[] sinks)
        {
            ImmutableArray<ISink> immutableSinks = sinks.ToImmutableArray();
            ImmutableInterlocked.InterlockedExchange(ref _activeSinks, immutableSinks);
        }
    }
}

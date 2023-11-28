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
            if (ImmutableInterlocked.InterlockedCompareExchange(ref _activeSinks, immutableSinks, ImmutableArray<ISink>.Empty) != ImmutableArray<ISink>.Empty)
            {
                throw new InvalidOperationException("FlyingLogs can only be configured once.");
            }
        }
    }
}

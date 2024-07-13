using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers
{
    internal class Constants
    {
        public static readonly LogLevel[] LogLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToArray();
        public static readonly (string? name, ITypeSymbol type)[] EmptyTypeSymbols = [];

        public static readonly string[] LoggableLevelNames = LogLevels
            .Where(l => l != LogLevel.None)
            .Select(l => l.ToString())
            .ToArray();

        public static readonly string[] ValidAccessExpressions = LoggableLevelNames
            .Select(n => "FlyingLogs.Log." + n)
            .ToArray();

        public static readonly Dictionary<string, LogLevel> NameToLogLevel = LogLevels
            .ToDictionary(l => l.ToString());
    }
}

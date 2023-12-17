using System.Collections.Immutable;

using FlyingLogs.Core;
using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

internal class CompilationDetails
{
    private static readonly LogLevel[] LogLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToArray();

    public INamedTypeSymbol? PreencodeAttributeType { get; private set; }
    public INamedTypeSymbol? IUtf8SpanFormattableType { get; private set; }
    public IMethodSymbol? IUtf8SpanFormattableTryFormatMethod { get; private set; }

    /// <summary>
    /// Contains log containing types such as FlyingLogs.Log.Error, FlyingLogs.Log.Trace etc.
    /// Index is based on enum <see cref="LogLevel"/> .
    /// </summary>
    public ImmutableArray<INamedTypeSymbol> LogMethodContainers { get; private set; }

    public static CompilationDetails Parse(Compilation c)
    {
        CompilationDetails result = new ()
        {
            PreencodeAttributeType = c.GetTypeByMetadataName(typeof(PreencodeAttribute).FullName),
            IUtf8SpanFormattableType = c.GetTypeByMetadataName("System.IUtf8SpanFormattable"),
        };

        result.IUtf8SpanFormattableTryFormatMethod = result.IUtf8SpanFormattableType?
            .GetMembers("TryFormat")
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        result.LogMethodContainers = ImmutableArray.Create(
            LogLevels.Select(l => c.GetTypeByMetadataName("FlyingLogs.Log." + l.ToString())!).ToArray()
        );

        return result;
    }

}
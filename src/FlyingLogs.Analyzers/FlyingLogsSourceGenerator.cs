using System.Collections.Immutable;
using System.Text;

using FlyingLogs.Analyzers.IncrementalValueProviders;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace FlyingLogs.Analyzers
{
    [Generator]
    public class FlyingLogsSourceGenerator : IIncrementalGenerator
    {
        private static readonly SourceText BasePartialTypeDeclarations = SourceText.From($@"
namespace FlyingLogs {{
    internal static partial class Log
    {{
        {string.Join("\n        ", Constants.LoggableLevelNames.Select(l => $"public static partial class {l} {{ }}"))}
    }}
}}", Encoding.UTF8);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("FlyingLogs.Log.g.cs", BasePartialTypeDeclarations);
            });

            var compilationDetails = context.CompilationProvider.Select((s, _) => CompilationDetails.Parse(s));

            var preencodeJsonProvider = PreencodeAttributeProvider.GetIfJsonPreencodingNeeded(context.SyntaxProvider);

            // Collect information about all log method calls.
            var logCallProvider = LogMethodProvider.GetValues(context.SyntaxProvider);

            // Each log method will need some string literals encoded as utf8.
            // Collect all the needed strings, removing duplicates.
            var stringLiterals = logCallProvider.Combine(preencodeJsonProvider).Select(
                ((ImmutableArray<LogMethodDetails> logs, bool preencodeJson) d, CancellationToken ct) =>
            {
                var result = new HashSet<string>();
                foreach (var builtInProperty in MethodBuilder.BuiltinPropertySerializers)
                    result.Add(builtInProperty.name);
                foreach (var level in Constants.LoggableLevelNames)
                    result.Add(level.ToString());
                foreach (var log in d.logs)
                {
                    foreach (var p in log!.Properties)
                        result.Add(p.Name);
                    foreach (var p in log.MessagePieces)
                        result.Add(p.Value);
                    result.Add(log.CalculateEventId().ToString());
                    result.Add(log.Template);
                }

                if (d.preencodeJson)
                {
                    // Most of the strings will be the same as their json encoded versions. Don't allocate too much.
                    List<string> jsons = new List<string>(1 + result.Count / 4);
                    foreach (var str in result)
                    {
                        var json = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(str);
                        if (string.CompareOrdinal(json, str) != 0 && result.Contains(json) == false)
                            jsons.Add(json);
                    }

                    foreach (var json in jsons)
                        result.Add(json);
                }

                return result;
            }).SelectMany((s, ct) => s);

            context.RegisterSourceOutput(stringLiterals, (scp, s) =>
            {
                string propertyName = MethodBuilder.GetPropertyNameForStringLiteral(s);
                string code = $$"""
namespace FlyingLogs
{
    internal static partial class Constants
    {
        public static readonly System.ReadOnlyMemory<byte> {{propertyName}} = new byte[] { {{string.Join(", ", Encoding.UTF8.GetBytes(s).Select(b => "(byte)0x" + b.ToString("x")))}} };
    }
}
""";
                scp.AddSource($"FlyingLogs.Constants.{propertyName}.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
            });

            context.RegisterSourceOutput(
                logCallProvider.SelectMany( (s,c) => s).Combine(preencodeJsonProvider),
                (spc, logsAndPreencoding) =>
                {
                    (LogMethodDetails log, bool preencodeJson) = logsAndPreencoding;
                    string filename = $"FlyingLogs.Log.{log!.Level}.{log.Name}.g.cs";
                    string code = preencodeJson ? MethodBuilder.BuildLogMethodJsonPreencoded(log) : MethodBuilder.BuildLogMethod(log);
                    spc.AddSource(filename, SourceText.From(code, Encoding.UTF8));
                }
            );

            context.RegisterSourceOutput(logCallProvider, NextAvailableMethodNameGenerator.GenerateNextAvailableMethodNameProperties);
        }
    }
}

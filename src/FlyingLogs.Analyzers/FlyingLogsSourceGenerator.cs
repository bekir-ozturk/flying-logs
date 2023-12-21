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

            var assemblyNameProvider = context.CompilationProvider.Select((s, _) => s.AssemblyName!);

            var preencodeJsonProvider = PreencodeAttributeProvider.GetIfJsonPreencodingNeeded(context.SyntaxProvider);

            // Collect information about all log method calls.
            var logCallProvider = LogMethodProvider.GetValues(context.SyntaxProvider);

            logCallProvider = logCallProvider
                .Combine(assemblyNameProvider)
                .Select(((ImmutableArray<LogMethodDetails> logs, string assemblyName) args, CancellationToken ct) =>
                {
                    int assemblyNameHash = Utilities.CalculateAssemblyNameHash(args.assemblyName);
                    for (int i=0; i < args.logs.Length; i++)
                    {
                        args.logs[i].EventId = 
                            (Utilities.CalculateAssemblyNameHash(args.logs[i].Name) ^ assemblyNameHash).ToString();
                    }

                    return args.logs;
                }
            );

            // Each log method will need some string literals encoded as utf8.
            // Collect all the needed strings, removing duplicates.
            var stringLiterals = logCallProvider.Combine(preencodeJsonProvider).Combine(assemblyNameProvider).Select(
                (((ImmutableArray<LogMethodDetails>, bool), string) e, CancellationToken ct) =>
            {
                ((ImmutableArray<LogMethodDetails> logs, bool preencodeJson), string assemblyName) = e;
                int assemblyNameHash = Utilities.CalculateAssemblyNameHash(assemblyName);

                var result = new HashSet<string>();
                foreach (var builtInProperty in MethodBuilder.BuiltinPropertySerializers)
                    result.Add(builtInProperty.name);
                foreach (var level in Constants.LoggableLevelNames)
                    result.Add(level.ToString());
                foreach (var log in logs)
                {
                    foreach (var p in log!.Properties)
                        result.Add(p.Name);
                    foreach (var p in log.MessagePieces)
                        result.Add(p.Value);
                    result.Add(log.EventId);
                    result.Add(log.Template);
                }

                if (preencodeJson)
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
                logCallProvider.SelectMany((s, c) => s).Combine(preencodeJsonProvider),
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

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

            var assemblyNameProvider = context.CompilationProvider.Select(
                (s, _) => Utilities.CalculateAssemblyNameHash(s.AssemblyName ?? ""));

            // Collect information about all log method calls.
            var logCallProvider = LogMethodProvider.GetValues(context.SyntaxProvider);

            logCallProvider = logCallProvider
                .Combine(assemblyNameProvider)
                .Select(((LogMethodDetails log, int assemblyNameHash) args, CancellationToken ct) =>
                {
                        args.log.EventId =
                            // Seq expects the event id in this format. No '0x' at the beginning.
                            (Utilities.CalculateAssemblyNameHash(args.log.Name) ^ args.assemblyNameHash).ToString("X");
                    return args.log;
                }
            );

            var logsCollectedProvider = logCallProvider.Collect();

            // Each log method will need some string literals encoded as utf8.
            // Collect all the needed strings, removing duplicates.
            var stringLiterals = logsCollectedProvider
                .Combine(assemblyNameProvider)
                .Select(((ImmutableArray<LogMethodDetails>, int) e, CancellationToken ct) =>
                {
                    (ImmutableArray<LogMethodDetails> logs, int assemblyNameHash) = e;

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

            // Report diagnostics.
            context.RegisterSourceOutput(
                logsCollectedProvider,
                (spc, logs) =>
                {
                    /* Method names should be unique not within the same Log level but across all log methods. This is
                     * because we don't take level into consideration when generating the event id. If two methods are
                     * created with the same name with different levels, they will have the same event id. We want to
                     * avoid that since we can't know whether the developer intentionally kept the name the same or
                     * reused the same method name by mistake.
                     * 
                     * Another reason to avoid duplicates is because they end up having the .cs same file name and then
                     * roslyn decides our generated code is not worth including in the compilation.
                     */
                    Dictionary<string, (LogMethodDetails details, bool alreadyReported)> uniqueLogs = new ();
                    foreach (var l in logs)
                    {
                        string key = l!.Name ?? "";
                        if (uniqueLogs.TryGetValue(key, out var method))
                        {
                            var loc = l.InvocationLocation;
                            spc.ReportDiagnostic(Diagnostic.Create(
                                Diagnostics.LogMethodNameIsNotUnique,
                                Location.Create(loc.Path,
                                    // TODO I'm not sure what I'm calculating here. Understand & cleanup.
                                    new TextSpan(loc.Span.Start.Line, loc.Span.End.Line - loc.Span.Start.Character),
                                    new LinePositionSpan(loc.StartLinePosition, loc.EndLinePosition)),
                                    l.Name));

                            if (!method.alreadyReported)
                            {
                                uniqueLogs[key] = (method.details, true);
                                loc = method.details.InvocationLocation;
                                spc.ReportDiagnostic(Diagnostic.Create(
                                Diagnostics.LogMethodNameIsNotUnique,
                                Location.Create(loc.Path,
                                    // TODO I'm not sure what I'm calculating here. Understand & cleanup.
                                    new TextSpan(loc.Span.Start.Line, loc.Span.End.Line - loc.Span.Start.Character),
                                    new LinePositionSpan(loc.StartLinePosition, loc.EndLinePosition)),
                                    l.Name));
                            }
                        }
                        else
                        {
                            uniqueLogs[key] = (l, false);
                        }

                        if (l.Diagnostic != null)
                        {
                            var loc = l.InvocationLocation;
                            object[] args = Array.Empty<object>();
                            if (l.DiagnosticArgument != null)
                                args = new [] { l.DiagnosticArgument };

                            spc.ReportDiagnostic(Diagnostic.Create(
                                l.Diagnostic,
                                Location.Create(l.InvocationLocation.Path,
                                    // TODO I'm not sure what I'm calculating here. Understand & cleanup.
                                    new TextSpan(loc.Span.Start.Line, loc.Span.End.Line - loc.Span.Start.Character),
                                    new LinePositionSpan(loc.StartLinePosition, loc.EndLinePosition)),
                                    args));
                        }
                    }
                }
            );

            context.RegisterSourceOutput(
                logCallProvider,
                (spc, log) =>
                {
                    string filename = $"FlyingLogs.Log.{log!.Level}.{log.Name}.g.cs";
                    string code = MethodBuilder.BuildLogMethod(log);
                    spc.AddSource(filename, SourceText.From(code, Encoding.UTF8));
                }
            );

            context.RegisterSourceOutput(logsCollectedProvider,
                NextAvailableMethodNameGenerator.GenerateNextAvailableMethodNameProperties);
        }
    }
}

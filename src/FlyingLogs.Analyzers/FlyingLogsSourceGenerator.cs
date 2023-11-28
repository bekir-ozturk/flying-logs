using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using System.Text;

namespace FlyingLogs.Analyzers
{
    [Generator]
    public class FlyingLogsSourceGenerator : IIncrementalGenerator
    {
        private static readonly LogLevel[] LogLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToArray();

        private static readonly string[] LoggableLevelNames = LogLevels
            .Where(l => l != LogLevel.None)
            .Select(l => l.ToString())
            .ToArray();

        private static readonly string[] ValidAccessExpressions = LoggableLevelNames
            .Select(n => "FlyingLogs.Log." + n)
            .ToArray();

        private static readonly Dictionary<string, LogLevel> NameToLogLevel = LogLevels
            .ToDictionary(l => l.ToString());

        private static readonly SourceText BasePartialTypeDeclarations = SourceText.From($@"
namespace FlyingLogs {{
    internal static partial class Log
    {{
        {string.Join("\n", LoggableLevelNames.Select(l => $"public static partial class {l} {{ }}"))}
    }}
}}", Encoding.UTF8);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("FlyingLogs.Log.g.cs", BasePartialTypeDeclarations);
            });

            var activeSinkProvider = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "FlyingLogs.UseSinkAttribute",
                    // Attribute should be marking the assembly.
                    predicate: (node, _) => node is CompilationUnitSyntax,
                    transform: SinkUsageIdentity.FromContext)
                .Where(static m => m is not null)!
                .Collect();

            var logCallProvider = context.SyntaxProvider.CreateSyntaxProvider(
                    (s, c) =>
                    {
                        /* Member access expression variables are named by the part of the expression string:
                         * "FlyingLogs.Log.Error.Method".
                         *            ^   ^     ^ 
                         *            |   |     |
                         *            |   |     errorMethodMemberAccess
                         *            |   logErrorMemberAccess
                         *            flyingLogsLogMemberAccess
                         */
                        if (s is InvocationExpressionSyntax invocationExpression
                            && invocationExpression.Expression is MemberAccessExpressionSyntax errorMethodMemberAccess
                            && errorMethodMemberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                        {
                            // Expression should again be member access since it will be Log.Error.
                            if (errorMethodMemberAccess.Expression is not MemberAccessExpressionSyntax logErrorAccess)
                                return false;

                            // logErrorExpression name should be Error, Warning, Information, Trace etc.
                            if (Array.IndexOf(LoggableLevelNames, logErrorAccess.Name.ToString()) == -1)
                                return false;

                            // Expression of logError can either be:
                            //      an identifier 'Log' or
                            //      another member access expression 'FlyingLogs.Log'.
                            if (logErrorAccess.Expression is IdentifierNameSyntax identifierName
                                && identifierName.Identifier.ToString() == "Log")
                            {
                                return true;
                            }
                            else if (logErrorAccess.Expression is MemberAccessExpressionSyntax flyingLogsMemberAccess
                                && flyingLogsMemberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                                && flyingLogsMemberAccess.Name.ToString() == "Log"
                                && flyingLogsMemberAccess.Expression is IdentifierNameSyntax flyingLogsSyntax
                                && flyingLogsSyntax.Identifier.ToString() == "FlyingLogs")
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        return false;
                    },
                    (c, ct) =>
                    {
                        if (c.Node is not InvocationExpressionSyntax invocationExpression
                            || invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccess
                            || memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression) == false)
                        {
                            return null;
                        }

                        string methodName = memberAccess.Name.ToString();
                        SyntaxNode levelSyntaxNode = ((MemberAccessExpressionSyntax)memberAccess.Expression).Name;

                        if (c.SemanticModel.GetSymbolInfo(levelSyntaxNode).Symbol is not INamedTypeSymbol logTypeSymbol
                            || Array.IndexOf(ValidAccessExpressions, logTypeSymbol.ToDisplayString()) == -1)
                        {
                            // The symbol resolves to a different 'Log' class that isn't related to our tool.
                            return null;
                        }

                        LogLevel logLevel = NameToLogLevel[logTypeSymbol.Name.ToString()];

                        Optional<object?> messageTemplate = null;
                        if (invocationExpression.ArgumentList.Arguments.Count != 0 &&
                            (messageTemplate = c.SemanticModel.GetConstantValue(invocationExpression.ArgumentList.Arguments[0].Expression)).HasValue)
                        {
                            return new LogMethodIdentity(logLevel, methodName, messageTemplate.Value as string ?? "");
                        }

                        return new LogMethodIdentity(logLevel, methodName, "");
                    })
                .Where(static m => m is not null)
                .Collect()
                .Combine(activeSinkProvider);

            context.RegisterSourceOutput(logCallProvider, (spc, s) =>
            {
                List<string> positionalFieldBuffer = new List<string>(4);
                Dictionary<LogLevel, StringBuilder> codeForLevel = new Dictionary<LogLevel, StringBuilder>();
                foreach (var level in LoggableLevelNames)
                {
                    StringBuilder? code = new StringBuilder();
                    codeForLevel[NameToLogLevel[level]] = code;
                    code.AppendLine("namespace FlyingLogs {")
                        .AppendLine("    internal static partial class Log {")
                        .Append("        public static partial class ").Append(level).AppendLine(" {");
                }

                foreach (var log in s.Left)
                {
                    StringBuilder? code = codeForLevel[log!.Level];

                    code.Append("            public static void ").Append(log.Name);
                    positionalFieldBuffer.Clear();
                    GetPositionalFields(log.Template, positionalFieldBuffer);

                    if (positionalFieldBuffer.Count > 0)
                    {
                        code.Append('<');
                        for (int i=0; i<positionalFieldBuffer.Count; i++)
                        {
                            if (i != 0)
                            {
                                code.Append(", ");
                            }

                            // This will start adding weird characters into the code if you go beyond 21 positional fields.
                            // This should be rare enough to ignore.
                            code.Append((char)('A' + i));
                        }
                        code.Append('>');
                    }
                    code.Append("(string messageTemplate");

                    for (int i = 0; i < positionalFieldBuffer.Count; i++)
                    {
                        code.Append(", ");
                        string field = positionalFieldBuffer[i];
                        code.Append((char)('A' + i)).Append(' ').Append(field);
                    }

                    code.AppendLine(") {");
                    MethodBuilder.Build(log, positionalFieldBuffer, code);
                    code.AppendLine("            }");
                }

                foreach(var levelCodes in codeForLevel)
                {
                    levelCodes.Value.AppendLine("}}}");
                    spc.AddSource(
                        "FlyingLogs.Log." + levelCodes.Key + ".g.cs",
                        SourceText.From(levelCodes.Value.ToString(), Encoding.UTF8));
                }
            });
        }

        private static void GetPositionalFields(string messageTemplate, List<string> fieldBuffer)
        {
            int head = 0;
            while (head <  messageTemplate.Length)
            {
                while (messageTemplate[head] != '{' || (head > 0 && messageTemplate[head-1] == '\\'))
                {
                    head++;
                    if (head == messageTemplate.Length)
                        return;
                }

                int startMarker = head;
                while (messageTemplate[head] != '}')
                {
                    head++;
                    if (head == messageTemplate.Length)
                        return;
                }

                int endMarker = head;
                fieldBuffer.Add(messageTemplate.Substring(startMarker + 1, endMarker - startMarker - 1));

                head++;
            }
        }
    }
}

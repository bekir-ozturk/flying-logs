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
        private static readonly ITypeSymbol[] EmptyTypeSymbols = new ITypeSymbol[0];

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
                        int argumentCount = invocationExpression.ArgumentList.Arguments.Count;
                        if (argumentCount != 0 &&
                            (messageTemplate = c.SemanticModel.GetConstantValue(invocationExpression.ArgumentList.Arguments[0].Expression)).HasValue)
                        {
                            var argList = invocationExpression.ArgumentList.Arguments;
                            ITypeSymbol[] argumentTypes = new ITypeSymbol[argumentCount - 1 /* exclude the first: message template*/];
                            for (int i=0; i<argumentTypes.Length; i++)
                            {
                                var arg = argList[i + 1 /* skip the message template */];
                                var typeInfo = c.SemanticModel.GetTypeInfo(arg.Expression);
                                if (typeInfo.Type is IErrorTypeSymbol || typeInfo.Type is null)
                                    continue;
                                argumentTypes[i] = typeInfo.Type;
                            }

                            return new LogMethodIdentity(logLevel, methodName, messageTemplate.Value as string ?? "", argumentTypes);
                        }

                        return new LogMethodIdentity(logLevel, methodName, "", EmptyTypeSymbols);
                    })
                .Where(static m => m is not null)
                .Collect();

            context.RegisterSourceOutput(logCallProvider, (spc, s) =>
            {
                Dictionary<LogLevel, StringBuilder> codeForLevel = new Dictionary<LogLevel, StringBuilder>();
                foreach (var level in LoggableLevelNames)
                {
                    StringBuilder? code = new StringBuilder();
                    codeForLevel[NameToLogLevel[level]] = code;
                    code.AppendLine("namespace FlyingLogs {")
                        .AppendLine("    internal static partial class Log {")
                        .Append("        public static partial class ").Append(level).AppendLine(" {");
                }

                foreach (var log in s)
                {
                    StringBuilder? code = codeForLevel[log!.Level];
                    LogMethodDetails? details = LogMethodDetails.Parse(log);

                    if (details == null)
                        continue; // Unable to parse. TODO display a warning.

                    code.Append("            public static void ").Append(log.Name).Append("(string messageTemplate");

                    for (int i = 0; i < details.Properties.Count; i++)
                    {
                        (string name, ITypeSymbol type) = details.Properties[i];
                        code.Append(", ").Append(type.ToDisplayString()).Append(' ').Append(name);
                    }

                    code.AppendLine(") {");
                    MethodBuilder.Build(details, code);
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

    }
}

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
        {string.Join("\n        ", LoggableLevelNames.Select(l => $"public static partial class {l} {{ }}"))}
    }}
}}", Encoding.UTF8);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("FlyingLogs.Log.g.cs", BasePartialTypeDeclarations);
            });

            // Collect information about all log method calls.
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
                            for (int i = 0; i < argumentTypes.Length; i++)
                            {
                                var arg = argList[i + 1 /* skip the message template */];
                                var typeInfo = c.SemanticModel.GetTypeInfo(arg.Expression);
                                if (typeInfo.Type is IErrorTypeSymbol || typeInfo.Type is null)
                                    continue;
                                argumentTypes[i] = typeInfo.Type;
                            }

                            // TODO eliminate LogMethodIdentity
                            return LogMethodDetails.Parse(new LogMethodIdentity(logLevel, methodName, messageTemplate.Value as string ?? "", argumentTypes));
                        }

                        return LogMethodDetails.Parse(new LogMethodIdentity(logLevel, methodName, "", EmptyTypeSymbols));
                    })
                .Where(static m => m is not null);

            // Each log method will need some string literals encoded as utf8.
            // Collect all the needed strings, removing duplicates.
            var stringLiterals = logCallProvider.Collect().Select((logs, ct) =>
            {
                var result = new HashSet<string>();
                foreach (var builtInProperty in MethodBuilder.BuiltinPropertySerializers)
                    result.Add(builtInProperty.name);
                foreach (var level in LogLevels)
                    result.Add(level.ToString());
                foreach (var log in logs)
                {
                    foreach (var p in log!.Properties)
                        result.Add(p.name);
                    foreach (var p in log.MessagePieces)
                        result.Add(p);
                    result.Add(log.CalculateEventId().ToString());
                    result.Add(log.Template);
                }

                return result;
            });

            context.RegisterSourceOutput(stringLiterals, (scp, s) =>
            {
                string code = $$"""
namespace FlyingLogs
{
    internal static class Constants
    {
{{      string.Join("\n", s.Select(s => $$"""
        public static readonly System.ReadOnlyMemory<byte> {{ MethodBuilder.GetPropertyNameForStringLiteral(s) }} = new byte[] { {{ string.Join(", ", Encoding.UTF8.GetBytes(s).Select(b => "(byte)0x" + b.ToString("x"))) }} };
""")) }}
    }
}
""";

                scp.AddSource("FlyingLogs.Constants.g.cs", SourceText.From(code.ToString(), Encoding.UTF8));
            });

            context.RegisterSourceOutput(logCallProvider, (spc, log) =>
            {
                string filename = $"FlyingLogs.Log.{log!.Level}.{log.Name}.g.cs";
                string code = MethodBuilder.BuildLogMethod(log);
                spc.AddSource(filename, SourceText.From(code, Encoding.UTF8));
            });
        }
    }
}

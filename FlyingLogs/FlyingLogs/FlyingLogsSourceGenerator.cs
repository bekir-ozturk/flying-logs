using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FlyingLogs
{
    [Generator]
    internal class FlyingLogsSourceGenerator : IIncrementalGenerator
    {
        private static string[] LevelNames =
        [
            "Trace",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Critical"
        ];

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(ctx =>
            {
                ctx.AddSource("FlyingLogs.Log.g.cs",
                    SourceText.From(@"namespace FlyingLogs {
    public static partial class Log
    {
        public static partial class Trace { private const string LevelName = ""Trace""; }
        public static partial class Debug { private const string LevelName = ""Debug""; }
        public static partial class Information { private const string LevelName = ""Information""; }
        public static partial class Warning { private const string LevelName = ""Warning""; }
        public static partial class Error { private const string LevelName = ""Error""; }
        public static partial class Critical { private const string LevelName = ""Critical""; }
    }
}
", Encoding.UTF8));
            });

            int n = 0;
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                    (s, c) =>
                    {
                        if (s is InvocationExpressionSyntax invocationExpression
                            && invocationExpression.Expression is MemberAccessExpressionSyntax memberAccess
                            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                        {
                            List<string> identifiers = new List<string>(4);
                            foreach (var n in memberAccess.ChildNodes())
                            {
                                if (identifiers.Count == 4)
                                    return false; // Too many identifiers.

                                if (n.IsKind(SyntaxKind.IdentifierName) == false)
                                    return false;

                                identifiers.Add(n.ToString());
                            }

                            if (identifiers.Count == 4 && identifiers[0] == "FlyingLogs" && identifiers[1] == "Log"
                                && Array.IndexOf(LevelNames, identifiers[2]) != -1)
                            {
                                return true;
                            }

                            if (identifiers.Count == 3 && identifiers[0] == "Log"
                                && Array.IndexOf(LevelNames, identifiers[1]) != -1)
                            {
                                return true;
                            }

                            return false;
                        }
                        return false;
                    },
                    (c, ct) =>
                    {
                        if (c.Node is InvocationExpressionSyntax invocationExpression
                            && invocationExpression.Expression is MemberAccessExpressionSyntax memberAccess
                            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                        {
                            string methodName = memberAccess.ChildNodes().Last().ToString();
                            List<SyntaxNode> identifierNodes = new List<SyntaxNode>(4);
                            foreach (var n in memberAccess.ChildNodes())
                            {
                                identifierNodes.Add(n);
                            }

                            SyntaxNode logNode = identifierNodes[identifierNodes.Count - 2];
                            if (c.SemanticModel.GetSymbolInfo(logNode).Symbol is not INamedTypeSymbol logTypeSymbol
                                || logTypeSymbol.MetadataName != "FlyingLogs.Log")
                            {
                                return null;
                            }

                            StringBuilder str = new StringBuilder();
                            str.AppendLine(memberAccess.ToString());
                            str.AppendLine(memberAccess.ChildNodes().Count().ToString());

                            int nodeIndex = 0;
                            foreach (var n in memberAccess.ChildNodes())
                            {
                                str.Append('\t')
                                    .Append(nodeIndex++)
                                    .Append(' ')
                                    .Append(n.Kind())
                                    .Append(' ')
                                    .AppendLine(n.ToString());
                            }

                            int argIndex = 0;
                            Optional<object?> messageTemplate = null;
                            str.AppendLine(invocationExpression.ArgumentList.Arguments.Count.ToString());
                            foreach (var a in invocationExpression.ArgumentList.Arguments)
                            {
                                if (argIndex == 0 && (messageTemplate = c.SemanticModel.GetConstantValue(a.Expression)).HasValue)
                                {
                                    str.AppendLine("ConstantValue: " + messageTemplate);
                                }
                                str.Append('\t')
                                    .Append(argIndex++)
                                    .Append(' ')
                                    .Append(a.Expression.Kind())
                                    .Append(' ')
                                    .AppendLine(a.ToString());
                            }

                            return str.ToString();
                        }
                        return "nothing";
                    }
                    )
                .Where(static m => m is not null);

            context.RegisterSourceOutput(provider, (spc, s) =>
            {
                spc.AddSource("a_" + (n++) + ".g.cs", SourceText.From(s, Encoding.UTF8));
            });
        }
    }
}

using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlyingLogs.Analyzers.IncrementalValueProviders
{
    internal class LogMethodProvider
    {
        public static IncrementalValuesProvider<LogMethodDetails> GetValues(
            SyntaxValueProvider syntaxProvider)
        {
            return syntaxProvider
                .CreateSyntaxProvider(FilterBySyntax, TransformToMemberAccessExpression)
                .Where(t => t != null)!;
        }

        private static bool FilterBySyntax(SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            /* Member access expression variables are named by the part of the expression string:
                         * "FlyingLogs.Log.Error.Method".
                         *            ^   ^     ^ 
                         *            |   |     |
                         *            |   |     errorMethodMemberAccess
                         *            |   logErrorMemberAccess
                         *            flyingLogsLogMemberAccess
                         */
            if (syntaxNode is InvocationExpressionSyntax invocationExpression
                && invocationExpression.Expression is MemberAccessExpressionSyntax errorMethodMemberAccess
                && errorMethodMemberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression))
            {
                // Expression should again be member access since it will be Log.Error.
                if (errorMethodMemberAccess.Expression is not MemberAccessExpressionSyntax logErrorAccess)
                    return false;

                // logErrorExpression name should be Error, Warning, Information, Trace etc.
                if (Array.IndexOf(Constants.LoggableLevelNames, logErrorAccess.Name.ToString()) == -1)
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
                    /* We can't validate the namespace identifier, because there can be an alias defined such as:
                     * > using SomeAlias = FlyingLogs; */
                    // && flyingLogsSyntax.Identifier.ToString() == "FlyingLogs"
                    )
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        private static LogMethodDetails?
            TransformToMemberAccessExpression(GeneratorSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.Node is not InvocationExpressionSyntax invocationExpression
                || invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression) == false)
            {
                return null;
            }

            var methodAccess = (MemberAccessExpressionSyntax)invocationExpression.Expression;
            string methodName = methodAccess.Name.ToString();
            SyntaxNode levelSyntaxNode = ((MemberAccessExpressionSyntax)methodAccess.Expression).Name;

            if (context.SemanticModel.GetSymbolInfo(levelSyntaxNode).Symbol is not INamedTypeSymbol logTypeSymbol
                || Array.IndexOf(Constants.ValidAccessExpressions, logTypeSymbol.ToDisplayString()) == -1)
            {
                // The symbol resolves to a different 'Log' class that isn't related to our tool.
                return null;
            }

            LogLevel logLevel = Constants.NameToLogLevel[logTypeSymbol.Name.ToString()];

            Optional<object?> messageTemplate;
            int argumentCount = invocationExpression.ArgumentList.Arguments.Count;
            if (argumentCount != 0 &&
                (messageTemplate = context.SemanticModel.GetConstantValue(invocationExpression.ArgumentList.Arguments[0].Expression)).HasValue)
            {
                var argList = invocationExpression.ArgumentList.Arguments;
                ITypeSymbol[] argumentTypes = new ITypeSymbol[argumentCount - 1 /* exclude the first: message template*/];
                for (int i = 0; i < argumentTypes.Length; i++)
                {
                    var arg = argList[i + 1 /* skip the message template */];
                    var typeInfo = context.SemanticModel.GetTypeInfo(arg.Expression);
                    if (typeInfo.Type is IErrorTypeSymbol || typeInfo.Type is null)
                        continue;
                    argumentTypes[i] = typeInfo.Type;
                }

                return LogMethodDetails.Parse(logLevel, methodName, messageTemplate.Value as string ?? "", argumentTypes);
            }

            return LogMethodDetails.Parse(logLevel, methodName, "", Constants.EmptyTypeSymbols);
        }
    }
}

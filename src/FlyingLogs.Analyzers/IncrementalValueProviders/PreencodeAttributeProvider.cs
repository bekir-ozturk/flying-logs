﻿using FlyingLogs.Core;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlyingLogs.Analyzers.IncrementalValueProviders
{
    internal class PreencodeAttributeProvider
    {
        public static IncrementalValueProvider<bool> GetIfJsonPreencodingNeeded(SyntaxValueProvider syntaxProvider)
        {
            return syntaxProvider.ForAttributeWithMetadataName(
                typeof(PreencodeAttribute).FullName,
                predicate: (node, _) => node is CompilationUnitSyntax,
                (ctx, t) => FindPreencodingRequests(ctx)
            )
            .SelectMany((rs, _) => rs)
            .Collect()
            .Select((rs, _) => rs.Any(r => r.encoding == LogEncoding.Utf8Json));
        }

        private static IEnumerable<(AttributeData attribute, LogEncoding encoding)> FindPreencodingRequests(
            GeneratorAttributeSyntaxContext ctx)
        {
            foreach (var att in ctx.Attributes)
            {
                var args = att.ConstructorArguments;
                if (args == null || args.Length != 1)
                    continue;

                if (args[0].Type?.ToDisplayString() ==  "FlyingLogs.Core." + nameof(LogEncoding)
                    && att.AttributeClass?.ToDisplayString() == "FlyingLogs.Core." + nameof(PreencodeAttribute))
                {
                    yield return (att, (LogEncoding)args[0].Value);
                }
            }
        }
    }
}
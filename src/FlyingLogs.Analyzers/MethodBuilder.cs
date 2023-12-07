using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlyingLogs.Analyzers
{
    internal class MethodBuilder
    {

        /* This should follow the exact order of members in LogProperty enum.
         * 
         * Assumes the following variables exist:
         * - b      : Memory<byte>     --> the buffer that the utf8 encoded strings will be writen into
         * - failed : bool             --> the flag that signals whether the serialization succeeded/failed.
         * - log    : LogMethodDetails --> the details of the log method that we are serializing.
        */
        public static readonly ImmutableArray<(string name, Func<LogMethodDetails, string> serializer)> BuiltinPropertySerializers = new (string, Func<LogMethodDetails, string>)[]
        {
            ("@t", l => $$"""
                        {
                
                            failed |= !DateTime.UtcNow.TryFormat(b.Span.Slice(offset), out int bytesWritten, "o", null);
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Timestamp] = (
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral("@t")}},
                                b.Slice(offset, bytesWritten)
                            );
                            offset += bytesWritten;
                        }
"""),
            ("@l", l => $$"""
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Level] = (
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral("@l")}},
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Level.ToString())}}
                            );
                        }
"""),
            ("@mt", l => $$"""
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.Template] = (
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral("@mt")}},
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Template)}}
                            );
                        }
"""),
            ("@i", l => $$"""
                        {
                            log.Properties[(int)FlyingLogs.Core.LogProperty.EventId] = (
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral("@i")}},
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.CalculateEventId().ToString())}}
                            );
                        }
"""),
        }.ToImmutableArray();

        public static string GetPropertyNameForStringLiteral(string str)
        {
            int croppedLength = Math.Min(128, str.Length);
            StringBuilder sb = new StringBuilder(croppedLength + 12);
            sb.Append('_'); // Even for empty strings, start with a valid character.
            for(int i=0; i<croppedLength; i++)
            {
                char c = str[i];
                sb.Append((c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c>= '0' && c<='9')
                    ? c
                    : '_');
            }
            sb.Append('_'); // Separate name and hash.
            int hashCode = str.GetHashCode();
            if (hashCode < 0)
            {
                sb.Append("_"); // Substitude minus sign with an underscore.
                // Get rid of the minus sign without risking overflow exception with Math.Abs(int.MinValue).
                hashCode ^= 1 << 31;
            }
            sb.Append(hashCode.ToString());
            return sb.ToString();
        }

        public static string BuildLogMethod(LogMethodDetails log)
        {
            return $$"""
namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class {{log.Level}}
        {
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> {{log.Name}}_pieces = new System.ReadOnlyMemory<byte>[] {
                {{ string.Join(", ", log.MessagePieces.Select(p => "FlyingLogs.Constants." + GetPropertyNameForStringLiteral(p))) }}
            };

            public static void {{log.Name}}(string template{{ string.Join("", log.Properties.Select(p => ", " + p.type.ToDisplayString() + " " + p.name)) }})
            {
                bool serialized = false;
                var log = FlyingLogs.Core.ThreadCache.RawLog.Value;
                var sinks = FlyingLogs.Configuration.ActiveSinks;
                var sinkCount = sinks.Length;

                for (int i=0; i < sinkCount; i++)
                {
                    if (sinks[i].IsLogLevelActive(FlyingLogs.Shared.LogLevel.{{log.Level}}) == false)
                        continue;

                    if (serialized == false)
                    {
                        log.Clear({{BuiltinPropertySerializers.Length}});
                
                        var b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                        int offset = 0;
                        var failed = false;

                        log.MessagePieces = {{log.Name}}_pieces;
                        log.PositionalPropertiesStartIndex = {{BuiltinPropertySerializers.Length}};
                        log.AdditionalPropertiesStartIndex = {{BuiltinPropertySerializers.Length}} + {{log.MessagePieces.Count - 1}};

{{                      string.Join("\n", BuiltinPropertySerializers.Select(s => s.serializer(log))) }}
{{                      GeneratePropertySerializers(log.Properties) }}

                        // TODO serialization logic

                        if (failed)
                        {
                            // TODO emit serialization failure metric
                            return;
                        }
                        serialized = true;
                    }

                    sinks[i].Ingest(log);
                }
            }
        }
    }
}
""";
        }
    
        private static string StringToLiteralExpression(string? str)
        {
            if (str == null)
                return "null";
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(str)).ToFullString();
        }

        private static string GeneratePropertySerializers(IEnumerable<(string name, ITypeSymbol type, string format)> properties)
        {
            StringBuilder str = new StringBuilder();
            foreach ((string name, ITypeSymbol type, string format) in properties)
            {
                if (type.AllInterfaces.Any( i => i.Name == "IUtf8SpanFormattable" && i.ContainingNamespace.Name == "System"))
                {
                    str.AppendLine($$"""
                        {
                            failed |= !{{ name }}.TryFormat(b.Span.Slice(offset), out int bytesWritten, {{ StringToLiteralExpression(format) }}, null);
                            log.Properties.Add((
                                FlyingLogs.Constants.{{ GetPropertyNameForStringLiteral(name) }},
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
""");
                }
                else
                {
                    // Fallback to ToString()
                    str.AppendLine($$"""
                        {
                            string ___value = {{ name }}.ToString({{ StringToLiteralExpression(format) }});
                            failed |= !System.Text.Encoding.UTF8.TryGetBytes(___value, b.Span.Slice(offset), out int bytesWritten);
                            log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(name)}},
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
""");
                }
            }

            return str.ToString();
        }
    }
}

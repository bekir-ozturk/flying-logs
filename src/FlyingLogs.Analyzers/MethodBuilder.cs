using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlyingLogs.Analyzers
{
    internal class MethodBuilder
    {

        /* This should follow the exact order of members in BuiltInProperty enum.
         * 
         * Assumes the following variables exist:
         * - b      : Memory<byte>     --> the buffer that the utf8 encoded strings will be written into
         * - failed : bool             --> the flag that signals whether the serialization succeeded/failed.
         * - log    : LogMethodDetails --> the details of the log method that we are serializing.
        */
        public static readonly ImmutableArray<(string name, Func<LogMethodDetails, string> serializer)> BuiltinPropertySerializers = new (string, Func<LogMethodDetails, string>)[]
        {
            ("@t", l => $$"""
                        {
                            failed |= !System.DateTime.UtcNow.TryFormat(b.Span.Slice(offset), out int bytesWritten, "o", null);
                            log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Timestamp] = b.Slice(offset, bytesWritten);
                            offset += bytesWritten;
                        }
"""),
            ("@l", l => $$"""
                        log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Level] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Level.ToString())}};
"""),
            ("@mt", l => $$"""
                        log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Template] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Template)}};
"""),
            ("@i", l => $$"""
                        log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.EventId] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.CalculateEventId().ToString())}};
"""),
        }.ToImmutableArray();

        public static readonly ImmutableArray<(string name, Func<LogMethodDetails, string> serializer)> BuiltinPropertyJsonOverrides = new (string, Func<LogMethodDetails, string>)[]
        {
            ("@mt", l => $$"""
                            log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Template] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Template)}};
"""),
        }.ToImmutableArray();

        public static string GetPropertyNameForStringLiteral(string str)
        {
            int croppedLength = Math.Min(128, str.Length);
            StringBuilder sb = new StringBuilder(croppedLength + 12);
            sb.Append('_'); // Even for empty strings, start with a valid character.
            for (int i = 0; i < croppedLength; i++)
            {
                char c = str[i];
                sb.Append((c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9')
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
                {{string.Join(", ", log.MessagePieces.Select(p => "FlyingLogs.Constants." + p.EncodedConstantPropertyName))}}
            };

            public static void {{log.Name}}(string template{{string.Join("", log.Properties.Select(p => ", " + p.TypeName + " " + p.Name))}})
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
                        log.Clear();
                        log.MessagePieces = {{log.Name}}_pieces;
                
                        var b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                        int offset = 0;
                        var failed = false;

{{string.Join("\n", BuiltinPropertySerializers.Select(s => s.serializer(log)))}}
{{GeneratePropertySerializers(log.Properties)}}

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

        public static string BuildLogMethodJsonPreencoded(LogMethodDetails log)
        {
            var escapedLog = log.CreateJsonEscapedClone(
                out bool templateChanged,
                out bool propertyNamesChanged,
                out bool piecesChanged);

            if (!templateChanged && !propertyNamesChanged && !piecesChanged)
                return BuildLogMethod(log); // Json encoding does not impact this log at all.

            string pieceList = $$"""
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> {{log.Name}}_pieces = new System.ReadOnlyMemory<byte>[] {
                {{string.Join(", ", log.MessagePieces.Select(p => "FlyingLogs.Constants." + p.EncodedConstantPropertyName))}}
            };
""";

            string pieceListJson = string.Empty;
            if (piecesChanged)
            {
                pieceListJson = $$"""
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> {{log.Name}}_json_pieces = new System.ReadOnlyMemory<byte>[] {
                {{string.Join(", ", escapedLog.MessagePieces.Select(p => "FlyingLogs.Constants." + p.EncodedConstantPropertyName))}}
            };
""";
            }

            return $$"""
namespace FlyingLogs
{
    internal static partial class Log
    {
        public static partial class {{log.Level}}
        {
{{          pieceList}}
{{          pieceListJson}}

            public static void {{log.Name}}(string template{{string.Join("", log.Properties.Select(p => ", " + p.TypeName + " " + p.Name))}})
            {
                bool serialized = false;
                bool jsonSerializationNeeded = false;
                var log = FlyingLogs.Core.ThreadCache.RawLog.Value;
                var sinks = FlyingLogs.Configuration.ActiveSinks;
                var sinkCount = sinks.Length;

                var b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                int offset = 0;

                for (int i=0; i < sinkCount; i++)
                {
                    var sink = sinks[i];
                    if (sink.IsLogLevelActive(FlyingLogs.Shared.LogLevel.{{log.Level}}) == false)
                        continue;

                    if (serialized == false)
                    {
                        log.Clear();
                        log.MessagePieces = {{log.Name}}_pieces;
                        bool failed = false;

{{                      string.Join("\n", BuiltinPropertySerializers.Select(s => s.serializer(log)))}}
{{                      GeneratePropertySerializers(log.Properties)}}

                        if (failed)
                        {
                            // TODO emit serialization failure metric
                            return;
                        }
                        serialized = true;
                    }

                    if (sink.ExpectedEncoding == FlyingLogs.Core.LogEncoding.Utf8Json)
                        jsonSerializationNeeded = true;
                    else
                    {
                        {{ /* The requested encoding is either Utf8Plain or it is something we aren't capable of providing. 
                            * Just provide Utf8Plain and let the middleware handle the encoding at runtime. */
                            string.Empty}}
                        sink.Ingest(log);
                    }
                }

                if (jsonSerializationNeeded)
                {
                    bool jsonSerialized = false;
                    for (int i=0; i < sinkCount; i++)
                    {
                        var sink = sinks[i];
                        if (sink.IsLogLevelActive(FlyingLogs.Shared.LogLevel.{{log.Level}}) == false)
                            continue;

                        if (sink.ExpectedEncoding != FlyingLogs.Core.LogEncoding.Utf8Json)
                            continue;

                        if (jsonSerialized == false)
                        {
                            bool failed = false;
                            {{ (piecesChanged ? $"log.MessagePieces = {log.Name}_pieces;" : string.Empty) }}
{{                      string.Join("\n", BuiltinPropertyJsonOverrides.Select(s => s.serializer(log))) }}
{{                      GeneratePropertyJsonOverriders(log.Properties, escapedLog.Properties) }}

                            if (failed)
                            {
                                // TODO emit serialization failure metric
                                return;
                            }
                            jsonSerialized = true;
                        }

                        sink.Ingest(log);
                    }
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
                return "(string?)null";
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(str)).ToFullString();
        }

        private static string GeneratePropertySerializers(IEnumerable<LogMethodProperty> properties)
        {
            StringBuilder str = new StringBuilder();
            foreach (var p in properties)
            {
                if (p.TypeSerialization == TypeSerializationMethod.ImplicitIUtf8SpanFormattable)
                {
                    str.AppendLine($$"""
                        {
                            failed |= !{{p.Name}}.TryFormat(b.Span.Slice(offset), out int bytesWritten, {{StringToLiteralExpression(p.Format)}}, null);
                            log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(p.Name)}},
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.ExplicitIUtf8SpanFormattable)
                {
                    str.AppendLine($$"""
                        {
                            failed |= !((System.IUtf8SpanFormattable){{p.Name}}).TryFormat(b.Span.Slice(offset), out int bytesWritten, {{StringToLiteralExpression(p.Format)}}, null);
                            log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(p.Name)}},
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
                            {{(
                                p.Format == null ?
                                $"string ___value = {p.Name}.ToString();" :
                                $"string ___value = {p.Name}.ToString({StringToLiteralExpression(p.Format)});"
                            )}}
                            failed |= !System.Text.Encoding.UTF8.TryGetBytes(___value, b.Span.Slice(offset), out int bytesWritten);
                            log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(p.Name)}},
                                b.Slice(offset, bytesWritten)
                            ));
                            offset += bytesWritten;
                        }
""");
                }
            }

            return str.ToString();
        }

        /// <summary>
        /// Generates a code that encodes the current value into Json and then overrides it with the encoded value.
        /// </summary>
        private static string GeneratePropertyJsonOverriders(List<LogMethodProperty> properties, List<LogMethodProperty> overrides)
        {
            StringBuilder str = new StringBuilder();
            for (int i = 0; i < properties.Count; i++)
            {
                str.AppendLine($$"""
                            {
                                failed |= System.Text.Encodings.Web.JavaScriptEncoder.Default.EncodeUtf8(log.Properties[{{i}}].value.Span, b.Span.Slice(offset), out int _, out int bytesWritten) != System.Buffers.OperationStatus.Done;
                                log.Properties[{{i}}] = (
                                    FlyingLogs.Constants.{{overrides[i].EncodedConstantPropertyName}},
                                    b.Slice(offset, bytesWritten)
                                );
                                offset += bytesWritten;
                            }
""");
            }

            return str.ToString();
        }
    }
}

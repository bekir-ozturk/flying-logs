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
         * - __b      : Memory<byte>     --> the buffer that the utf8 encoded strings will be written into
         * - __failed : bool             --> the flag that signals whether the serialization succeeded/failed.
         * - __log    : LogMethodDetails --> the details of the log method that we are serializing.
        */
        public static readonly ImmutableArray<(string name, Func<LogMethodDetails, string> serializer)> BuiltinPropertySerializers = new (string, Func<LogMethodDetails, string>)[]
        {
            ("@t", l => $$"""
                        {
                            __failed |= !System.DateTime.UtcNow.TryFormat(__b.Span.Slice(__offset), out int __bytesWritten, "o", null);
                            __log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Timestamp] = __b.Slice(__offset, __bytesWritten);
                            __offset += __bytesWritten;
                        }
"""),
            ("@l", l => $$"""
                        __log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Level] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Level.ToString())}};
"""),
            ("@mt", l => $$"""
                        __log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Template] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Template)}};
"""),
            ("@i", l => $$"""
                        __log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.EventId] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.CalculateEventId().ToString())}};
"""),
        }.ToImmutableArray();

        public static readonly ImmutableArray<(string name, Func<LogMethodDetails, string> serializer)> BuiltinPropertyJsonOverrides = new (string, Func<LogMethodDetails, string>)[]
        {
            ("@mt", l => $$"""
                            __log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Template] = FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(l.Template)}};
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
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> __{{log.Name}}_pieces = new System.ReadOnlyMemory<byte>[] {
                {{string.Join(", ", log.MessagePieces.Select(p => "FlyingLogs.Constants." + p.EncodedConstantPropertyName))}}
            };

            public static void {{log.Name}}(string template{{string.Join("", log.Properties.Select(p => ", " + p.TypeName + " " + p.Name))}})
            {
                bool __serialized = false;
                var __log = FlyingLogs.Core.ThreadCache.RawLog.Value;
                var __sinks = FlyingLogs.Configuration.ActiveSinks;
                var __sinkCount = __sinks.Length;

                for (int __i=0; __i < __sinkCount; __i++)
                {
                    if (__sinks[__i].IsLogLevelActive(FlyingLogs.Shared.LogLevel.{{log.Level}}) == false)
                        continue;

                    if (__serialized == false)
                    {
                        __log.Clear();
                        __log.MessagePieces = __{{log.Name}}_pieces;
                
                        var __b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                        int __offset = 0;
                        var __failed = false;

{{string.Join("\n", BuiltinPropertySerializers.Select(s => s.serializer(log)))}}
{{GeneratePropertySerializers(log.Properties)}}

                        if (__failed)
                        {
                            // TODO emit serialization failure metric
                            return;
                        }
                        __serialized = true;
                    }

                    __sinks[__i].Ingest(__log);
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
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> __{{log.Name}}_pieces = new System.ReadOnlyMemory<byte>[] {
                {{string.Join(", ", log.MessagePieces.Select(p => "FlyingLogs.Constants." + p.EncodedConstantPropertyName))}}
            };
""";

            string pieceListJson = string.Empty;
            if (piecesChanged)
            {
                pieceListJson = $$"""
            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> __{{log.Name}}_json_pieces = new System.ReadOnlyMemory<byte>[] {
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
                bool __serialized = false;
                bool __jsonSerializationNeeded = false;
                var __log = FlyingLogs.Core.ThreadCache.RawLog.Value;
                var __sinks = FlyingLogs.Configuration.ActiveSinks;
                var __sinkCount = __sinks.Length;

                var __b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                int __offset = 0;

                for (int __i=0; __i < __sinkCount; __i++)
                {
                    var __sink = __sinks[__i];
                    if (__sink.IsLogLevelActive(FlyingLogs.Shared.LogLevel.{{log.Level}}) == false)
                        continue;

                    if (__serialized == false)
                    {
                        __log.Clear();
                        __log.MessagePieces = __{{log.Name}}_pieces;
                        bool __failed = false;

{{                      string.Join("\n", BuiltinPropertySerializers.Select(s => s.serializer(log)))}}
{{                      GeneratePropertySerializers(log.Properties)}}

                        if (__failed)
                        {
                            // TODO emit serialization failure metric
                            return;
                        }
                        __serialized = true;
                    }

                    if (__sink.ExpectedEncoding == FlyingLogs.Core.LogEncoding.Utf8Json)
                        __jsonSerializationNeeded = true;
                    else
                    {
                        {{ /* The requested encoding is either Utf8Plain or it is something we aren't capable of providing. 
                            * Just provide Utf8Plain and let the middleware handle the encoding at runtime. */
                            string.Empty}}
                        __sink.Ingest(__log);
                    }
                }

                if (__jsonSerializationNeeded)
                {
                    bool __jsonSerialized = false;
                    for (int __i=0; __i < __sinkCount; __i++)
                    {
                        var __sink = __sinks[__i];
                        if (__sink.IsLogLevelActive(FlyingLogs.Shared.LogLevel.{{log.Level}}) == false)
                            continue;

                        if (__sink.ExpectedEncoding != FlyingLogs.Core.LogEncoding.Utf8Json)
                            continue;

                        if (__jsonSerialized == false)
                        {
                            bool __failed = false;
                            {{ (piecesChanged ? $"__log.MessagePieces = __{log.Name}_pieces;" : string.Empty) }}
{{                      string.Join("\n", BuiltinPropertyJsonOverrides.Select(s => s.serializer(log))) }}
{{                      GeneratePropertyJsonOverriders(log.Properties, escapedLog.Properties) }}

                            if (__failed)
                            {
                                // TODO emit serialization failure metric
                                return;
                            }
                            __jsonSerialized = true;
                        }

                        __sink.Ingest(__log);
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
                            __failed |= !{{p.Name}}.TryFormat(__b.Span.Slice(__offset), out int __bytesWritten, {{StringToLiteralExpression(p.Format)}}, null);
                            __log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(p.Name)}},
                                __b.Slice(__offset, __bytesWritten)
                            ));
                            __offset += __bytesWritten;
                        }
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.ExplicitIUtf8SpanFormattable)
                {
                    str.AppendLine($$"""
                        {
                            __failed |= !((System.IUtf8SpanFormattable){{p.Name}}).TryFormat(__b.Span.Slice(offset), out int __bytesWritten, {{StringToLiteralExpression(p.Format)}}, null);
                            __log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(p.Name)}},
                                __b.Slice(__offset, __bytesWritten)
                            ));
                            __offset += __bytesWritten;
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
                                $"string __value = {p.Name}.ToString();" :
                                $"string __value = {p.Name}.ToString({StringToLiteralExpression(p.Format)});"
                            )}}
                            __failed |= !System.Text.Encoding.UTF8.TryGetBytes(__value, __b.Span.Slice(__offset), out int __bytesWritten);
                            __log.Properties.Add((
                                FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(p.Name)}},
                                __b.Slice(__offset, __bytesWritten)
                            ));
                            __offset += __bytesWritten;
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
                                __failed |= System.Text.Encodings.Web.JavaScriptEncoder.Default.EncodeUtf8(__log.Properties[{{i}}].value.Span, __b.Span.Slice(__offset), out int _, out int __bytesWritten) != System.Buffers.OperationStatus.Done;
                                __log.Properties[{{i}}] = (
                                    FlyingLogs.Constants.{{overrides[i].EncodedConstantPropertyName}},
                                    __b.Slice(__offset, __bytesWritten)
                                );
                                __offset += __bytesWritten;
                            }
""");
            }

            return str.ToString();
        }
    }
}

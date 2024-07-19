using System.Collections.Immutable;
using System.Diagnostics;
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
                    __failed |= !System.DateTime.UtcNow.TryFormat(__b.Span.Slice(__offset), out __bytesWritten, "o", null);
                    __log.BuiltinProperties[(int)FlyingLogs.Core.BuiltInProperty.Timestamp] = __b.Slice(__offset, __bytesWritten);
                    __offset += __bytesWritten;
                }
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
            int hashCode = str.GetDeterministicHashCode();
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
            // TODO: for empty arrays, use  Array.Empty.
            return $$"""
#nullable disable
#pragma warning disable CS8669,CS0219
namespace FlyingLogs
{
    file static class Templates
    {
        public static readonly FlyingLogs.Core.LogTemplate Utf8Plain = new (
            Level: FlyingLogs.Shared.LogLevel.{{log.Level}},
            EventId: FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(log.EventId)}},
            TemplateString: FlyingLogs.Constants.{{GetPropertyNameForStringLiteral(log.Template)}},
            MessagePieces: new System.ReadOnlyMemory<byte>[] {
                {{ string.Join(",\n                ", log.MessagePieces.Select(p => "FlyingLogs.Constants." + p.EncodedConstantPropertyName))}}
            },
            PropertyNames: new System.ReadOnlyMemory<byte>[] {
                {{ string.Join(",\n                ", log.Properties.Select(p => "FlyingLogs.Constants." + GetPropertyNameForStringLiteral(p.Name)))}}
            },
            PropertyTypes: new FlyingLogs.Shared.BasicPropertyType[] {
                {{ string.Join(",\n                ", log.Properties.Select(p => "FlyingLogs.Shared.BasicPropertyType." + p.OutputType)) }}
            },
            PropertyDepths: new byte[] {
                {{ string.Join(", ", log.Properties.Select(p => p.Depth)) }}
            }
        );
    }

    internal static partial class Log
    {
        public static partial class {{log.Level}}
        {
            public static void {{log.Name}}(string template{{string.Join("", log.Properties.Where(l=> l.Depth == 0).Select(p => ", " + p.TypeName + " " + p.Name))}})
            {
                var __config = FlyingLogs.Configuration.Current;
                if (__config.MinimumLevelOfInterest > FlyingLogs.Shared.LogLevel.{{log.Level}})
                    return;

                var __values = FlyingLogs.Core.ThreadCache.PropertyValuesTemp.Value!;
                var __b = FlyingLogs.Core.ThreadCache.Buffer.Value;
                int __offset = 0;
                int __bytesWritten = 0;
                var __failed = false;

                __values.Clear();
{{              GeneratePropertySerializers(log.Properties)}}

                if (__failed)
                {
                    FlyingLogs.Core.Metrics.SerializationError.Add(1);
                    // Failure shouldn't break the data, we just have less of it available. Continue and pour.
                }

                FlyingLogs.Configuration.Pour(__config, Templates.Utf8Plain, __values, __b.Slice(__offset));
            }
        }
    }
}
""";
        }

        private static string StringToLiteralExpression(string? str)
        {
            if (str == null)
                return "(string)null";
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(str)).ToFullString();
        }

        private static string GeneratePropertySerializers(IReadOnlyList<LogMethodProperty> properties)
        {
            StringBuilder str = new StringBuilder();
            Stack<int> activeNullCheckDepths = new();
            // Stores the identifier tokens to be used when accessing the property. If property is 'Player.Position.Z',
            // then the stack contains ["Player", "Position", "Z"].
            Stack<string> propertyAccessSyntax = new();
            propertyAccessSyntax.Push(string.Empty);
            int lastPropertyDepth = 0;

            for (int i=0; i<properties.Count; i++)
            {
                var p = properties[i];
                if (lastPropertyDepth >= p.Depth)
                {
                    propertyAccessSyntax.Pop();
                    while (lastPropertyDepth-- > p.Depth)
                        propertyAccessSyntax.Pop();
                }

                lastPropertyDepth = p.Depth;

                propertyAccessSyntax.Push(p.Name + p.PropertyAccessorPostfix);
                string propertyAccessor = string.Join(".", propertyAccessSyntax.Reverse());
                // Here comes the first hack of the codebase. I question whether I really need this feature this much,
                // enough to subject the codebase to... this.
                // We use a separate accessor when checking for null. This is solely to allow logging nullable value
                // types such as int?. For a nullable int 'boo', we do null checks using 'boo == null' whereas to get
                // the value itself we use 'boo.Value'.
                string propertyAccessorForNullCheck = propertyAccessor.Substring(0
                    , propertyAccessor.Length - p.PropertyAccessorPostfix.Length);

                while (activeNullCheckDepths.Count > 0 && activeNullCheckDepths.Peek() >= p.Depth)
                {
                    _ = activeNullCheckDepths.Pop();
                    str.AppendLine("                }");
                }

                // TODO p.format should be used for renderings. internal format should be used for generating the
                // original Utf8Plain data. The two can differ. For instance, for DateTime
                // internalFormat: s (always output datetime in the same sortable format regardless of culture)
                // format: DDMMYYYY (user can ask to see it in a different way)
                string? internalFormat = null;

                if (p.IsNullable)
                {
                    activeNullCheckDepths.Push(p.Depth);
                    if (p.TypeSerialization == TypeSerializationMethod.Complex)
                    {
                        int skippedPropCount = 0;
                        for (int j=i+1; j<properties.Count; j++, skippedPropCount++)
                        {
                            if (properties[j].Depth <= p.Depth)
                                break;
                        }

                        str.AppendLine($$"""
                if ({{propertyAccessorForNullCheck}} == null)
                {
                    __values.Add(FlyingLogs.Shared.PropertyValueHints.ComplexNull);
""");

                        for (int j=i+1; j<properties.Count && properties[j].Depth > p.Depth; j++)
                        {
                            str.AppendLine("""
                    __values.Add(FlyingLogs.Shared.PropertyValueHints.Skip);
""");
                        }
                        str.AppendLine("""
                }
                else
                {
""");
                    }
                    else
                    {
                        str.AppendLine($$"""
                if ({{propertyAccessorForNullCheck}} == null)
                    __values.Add(FlyingLogs.Shared.PropertyValueHints.Null);
                else
                {
""");
                    }
                }

                if (p.TypeSerialization == TypeSerializationMethod.ImplicitIUtf8SpanFormattable)
                {
                    str.AppendLine($$"""
                __failed |= !{{propertyAccessor}}.TryFormat(__b.Span.Slice(__offset), out __bytesWritten, {{StringToLiteralExpression(internalFormat)}}, null);
                __values.Add(__b.Slice(__offset, __bytesWritten));
                __offset += __bytesWritten;
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.ExplicitIUtf8SpanFormattable)
                {
                    str.AppendLine($$"""
                __failed |= !((System.IUtf8SpanFormattable){{propertyAccessor}}).TryFormat(__b.Span.Slice(__offset), out __bytesWritten, {{StringToLiteralExpression(internalFormat)}}, null);
                __values.Add(__b.Slice(__offset, __bytesWritten));
                __offset += __bytesWritten;
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.None
                    || p.TypeSerialization == TypeSerializationMethod.ToString)
                {
                    str.AppendLine($$"""
                __failed |= !System.Text.Encoding.UTF8.TryGetBytes({{(
                    p.TypeSerialization == TypeSerializationMethod.None
                    ? $"{propertyAccessor}"
                    : (internalFormat == null
                        ? $"{propertyAccessor}.ToString()"
                        : $"{propertyAccessor}.ToString({StringToLiteralExpression(internalFormat)})")
                    )}}, __b.Span.Slice(__offset), out __bytesWritten);
                __values.Add(__b.Slice(__offset, __bytesWritten));
                __offset += __bytesWritten;
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.Complex)
                {
                    str.AppendLine($$"""
                __values.Add(FlyingLogs.Shared.PropertyValueHints.Complex);
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.Bool)
                {
                    str.AppendLine($$"""
                {
                    System.ReadOnlyMemory<byte> __preencodedValue = {{propertyAccessor}}
                        ? FlyingLogs.Core.Constants.BoolTrueUtf8Plain
                        : FlyingLogs.Core.Constants.BoolFalseUtf8Plain;
                    __values.Add(__preencodedValue);
                    __offset += __preencodedValue.Length;
                }
""");
                }
                else if (p.TypeSerialization == TypeSerializationMethod.DateTime)
                {
                    str.AppendLine($$"""
                __failed |= !{{propertyAccessor}}.TryFormat(__b.Span.Slice(__offset), out __bytesWritten, "O", null);
                __values.Add(__b.Slice(__offset, __bytesWritten));
                __offset += __bytesWritten;
""");
                }
                else
                {
                    // A new serialization method that we haven't considered here. We need to update this code.
                    Debug.Assert(false);
                }
            }

            for (int i=0; i<activeNullCheckDepths.Count; i++)
                str.AppendLine("                }");

            return str.ToString();
        }
    }
}

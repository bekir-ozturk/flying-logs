using System.Text;
using System.Text.Encodings.Web;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers
{
    internal class MethodBuilder
    {
        public static string GetPropertyNameForStringLiteral(string str)
        {
            StringBuilder sb = new StringBuilder(str.Length + 12);
            foreach (char c in str)
            {
                sb.Append((c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')
                    ? c
                    : '_');
            }
            sb.Append(str.GetHashCode());
            return sb.ToString();
        }

        public static string BuildLogMethod(LogMethodDetails log)
        {
            StringBuilder? output = new StringBuilder();
            output.AppendLine("namespace FlyingLogs {")
                .AppendLine("    internal static partial class Log {")
                .Append("        public static partial class ").Append(log.Level).AppendLine(" {");
            output.Append("            private static readonly ImmutableArray<ReadOnlyMemory<byte>> ")
                .Append(log.Name).Append("_pieces = new {}");
 
            output.Append("            public static void ").Append(log.Name).Append("(string messageTemplate");

            foreach ((string name, ITypeSymbol type, string _) in log.Properties)
            {
                output.Append(", ").Append(type.ToDisplayString()).Append(' ').Append(name);
            }

            output.AppendLine(") {")
                .AppendLine("bool serialized = false;")
                .AppendLine("var sinks = FlyingLogs.Core.Configuration.ActiveSinks;")
                .AppendLine("var sinkCount = sinks.Count;")
                .AppendLine("for(int i = 0; i<sinkCount; i++ ) {")
                .AppendLine($"    if (sinks[i].LogLevelActive(FlyingLogs.Shared.LogLevel.{log.Level}))")
                .AppendLine("    {")
                .AppendLine("        if (serialized == false) {")
                .AppendLine("FlyingLogs.Core.Configuration.RawLog.Clear();")
                .AppendLine("TODO serialization logic")
                .AppendLine("serialized = true;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        sinks[i].Ingest(FlyingLogs.Core.Configuration.RawLog);")
                .AppendLine("    }") // close if
                .AppendLine("}") // close for loop
                .AppendLine("            }") // close method definition
                .AppendLine("}}}"); // close class definitions Log.Level and namespace.

            return output.ToString();


            string eventId = log.GetHashCode().ToString();
            string templateEscaped = JavaScriptEncoder.Default.Encode(log.ToString());

            output.Append("var buffer = ").Append(_sinkTypeName).AppendLine(".Instance.Buffer;");
            output.AppendLine("string ___t = System.DateTime.UtcNow.ToString(\"s\");");

            for (int i = 0; i < positionalFields.Count; i++)
            {
                output.Append("string f_").Append(i).Append(" = ")
                    .Append(positionalFields[i]).AppendLine(".ToString();");
            }

            output.Append("int bytesNeeded = ").Append(Encoding.UTF8.GetByteCount("{{\"@mt\":\"\",\"@i\":\"\",\"@t\":\"\"}}")).AppendLine()
                .Append("    + ").Append(
                    Encoding.UTF8.GetByteCount(templateEscaped) 
                    + Encoding.UTF8.GetByteCount(eventId)
                    + Encoded.JsonPartsLevel[log.Level].Length).AppendLine()
                .AppendLine("    + System.Text.Encoding.UTF8.GetByteCount(___t)");

            for (int i=0; i<positionalFields.Count; i++)
            {
                output.Append("    + ").Append(Encoding.UTF8.GetByteCount(positionalFields[i])).Append(" + System.Text.Encoding.UTF8.GetByteCount(").Append("f_").Append(i).AppendLine(")");
            }
            output.AppendLine(";").AppendLine();


        }
    }
}

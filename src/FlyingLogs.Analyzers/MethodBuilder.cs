using System.Text;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers
{
    internal class MethodBuilder
    {
        public static string GetPropertyNameForStringLiteral(string str)
        {
            int croppedLength = Math.Min(128, str.Length);
            StringBuilder sb = new StringBuilder(croppedLength + 12);
            sb.Append('_');
            for(int i=0; i<croppedLength; i++)
            {
                char c = str[i];
                sb.Append((c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')
                    ? c
                    : '_');
            }
            int hashCode = str.GetHashCode();
            if (hashCode < 0)
            {
                sb.Append("_");
                // Get rid of the minus sign without risking overflow exception with Math.Abs(int.MinValue).
                hashCode ^= 1 << 31;
            }
            sb.Append(hashCode.ToString());
            return sb.ToString();
        }

        public static string BuildLogMethod(LogMethodDetails log)
        {
            StringBuilder? output = new StringBuilder();
            output.AppendLine("namespace FlyingLogs {")
                .AppendLine(  "    internal static partial class Log {")
                .Append(      "        public static partial class ").AppendLine(log.Level.ToString())
                .AppendLine(  "        {")
                .Append(      "            private static readonly System.ReadOnlyMemory<System.ReadOnlyMemory<byte>> ")
                .Append(log.Name)
                .AppendLine("_pieces = new System.ReadOnlyMemory<byte>[]{");
                
            foreach(var piece in log.MessagePieces)
            {
                string pieceArrayName = GetPropertyNameForStringLiteral(piece);
                output.Append("                FlyingLogs.Constants.").Append(pieceArrayName).AppendLine(", ");
            }

            output.AppendLine("            };")
                .AppendLine();
 
            output.Append("            public static void ").Append(log.Name).Append("(string messageTemplate");

            foreach ((string name, ITypeSymbol type, string _) in log.Properties)
            {
                output.Append(", ").Append(type.ToDisplayString()).Append(' ').Append(name);
            }

            output.AppendLine(") {")
                .AppendLine("bool serialized = false;")
                .AppendLine("var sinks = FlyingLogs.Configuration.ActiveSinks;")
                .AppendLine("var sinkCount = sinks.Length;")
                .AppendLine("for(int i = 0; i<sinkCount; i++ ) {")
                .AppendLine($"    if (sinks[i].IsLogLevelActive(FlyingLogs.Shared.LogLevel.{log.Level}))")
                .AppendLine("    {")
                .AppendLine("        if (serialized == false) {")
                .AppendLine("            FlyingLogs.Configuration.RawLog.Clear();")
                .AppendLine("            // TODO serialization logic")
                .AppendLine("            serialized = true;")
                .AppendLine("        }")
                .AppendLine()
                .AppendLine("        sinks[i].Ingest(FlyingLogs.Configuration.RawLog);")
                .AppendLine("    }") // close if
                .AppendLine("}") // close for loop
                .AppendLine("            }") // close method definition
                .AppendLine("}}}"); // close class definitions Log.Level and namespace.

            return output.ToString();


           /*  string eventId = log.GetHashCode().ToString();
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

 */
        }
    }
}

using System.Text;
using System.Text.Encodings.Web;

using FlyingLogs.Shared;

namespace FlyingLogs.Analyzers
{
    internal class MethodBuilder
    {
        private static readonly string _sinkTypeName = "FlyingLogs.Sinks.SeqHttpSink";

        public static void Build(LogMethodIdentity log, List<string> positionalFields, StringBuilder output)
        {
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

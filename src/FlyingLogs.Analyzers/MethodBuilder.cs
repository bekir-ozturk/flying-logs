using System.Text;
using System.Text.Encodings.Web;

using FlyingLogs.Shared;

namespace FlyingLogs.Analyzers
{
    internal class MethodBuilder
    {
        public static void Build(LogMethodDetails log, StringBuilder output)
        {
          code.AppendLine("bool serialized = false;");
          code.AppendLine("var sinks = FlyingLogs.Core.Configuration.ActiveSinks;");
          code.AppendLine("var sinkCount = sinks.Count;");
          code.AppendLine("for(int i = 0; i<sinkCount; i++ ) {")
          code.AppendLine($"    if (sinks[i].LogLevelActive(FlyingLogs.Shared.LogLevel.{log.Level}))")
          code.AppendLine("    {")
              .AppendLine("        if (serialized == false) {")
              .AppendLine("FlyingLogs.Core.Configuration.RawLog.Clear();")
              .AppendLine("TODO serialization logic")
              .AppendLine("serialized = true;")
              .AppendLine("        }")
              .AppendLine()
              .AppendLine("        sinks[i].Ingest(FlyingLogs.Core.Configuration.RawLog);")
              .AppendLine("    }")
          RawLog result = new();
          
          
          
          
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

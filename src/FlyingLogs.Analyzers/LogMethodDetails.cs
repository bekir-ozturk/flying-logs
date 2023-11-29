using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers
{
    internal class LogMethodDetails
    {
        public LogLevel Level { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public List<(string name, ITypeSymbol type)> Properties { get; set; }
        public List<(string piece, int propertyIndex)> MessagePieces { get; set; }

        public LogMethodDetails(LogLevel level, string name, string template, List<(string name, ITypeSymbol type)> properties, List<(string piece, int propertyIndex)> messagePieces)
        {
            Level = level;
            Name = name;
            Template = template;
            Properties = properties;
            MessagePieces = messagePieces;
        }

        internal static LogMethodDetails? Parse(LogMethodIdentity identity)
        {


            return new LogMethodDetails(identity.Level, identity.Name, identity.Template, properties, messagePieces);
        }

        private static void GetPositionalFields(string messageTemplate, List<string> fieldBuffer)
        {
            int head = 0;
            while (head < messageTemplate.Length)
            {
                while (messageTemplate[head] != '{' || (head > 0 && messageTemplate[head - 1] == '\\'))
                {
                    head++;
                    if (head == messageTemplate.Length)
                        return;
                }

                int startMarker = head;
                while (messageTemplate[head] != '}')
                {
                    head++;
                    if (head == messageTemplate.Length)
                        return;
                }

                int endMarker = head;
                fieldBuffer.Add(messageTemplate.Substring(startMarker + 1, endMarker - startMarker - 1));

                head++;
            }
        }
    }
}

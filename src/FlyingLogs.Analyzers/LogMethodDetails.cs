using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers
{
    internal class LogMethodDetails
    {
        public LogLevel Level { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public List<(string name, ITypeSymbol type, string format)> Properties { get; set; }
        public List<(string piece, int propertyIndex)> MessagePieces { get; set; }

        public LogMethodDetails(
            LogLevel level,
            string name,
            string template,
            List<(string name, ITypeSymbol type)> properties,
            List<(string piece, int propertyIndex, string format)> messagePieces)
        {
            Level = level;
            Name = name;
            Template = template;
            Properties = properties;
            MessagePieces = messagePieces;
        }

        internal static LogMethodDetails? Parse(LogMethodIdentity identity)
        {
            int tail = 0;
            var propertyLocations = GetPositionalFields(identity.Template);
            if (propertyLocations.Count != identity.ArgumentTypes.Length)
            {
                // The number of properties in the template doesn't match the number of arguments passed.
                return null;
            }

            foreach(var (start, end) in propertyLocations)
            {
                string piece = identity.Template.Substring(tail, start - tail - 1);

            }

            return new LogMethodDetails(identity.Level, identity.Name, identity.Template, properties, messagePieces);
        }

        private static List<(int start, int end)> GetPositionalFields(string messageTemplate)
        {
            List<(int start, int end)> props = new(4);
            int head = 0;
            while (head < messageTemplate.Length)
            {
                while (messageTemplate[head] != '{' || (head > 0 && messageTemplate[head - 1] == '\\'))
                {
                    head++;
                    if (head == messageTemplate.Length)
                        return props;
                }

                int startMarker = head;
                while (messageTemplate[head] != '}')
                {
                    head++;
                    if (head == messageTemplate.Length)
                        return props;
                }

                int endMarker = head;
                props.Add((startMarker + 1, endMarker));

                head++;
            }

            return props;
        }
    }
}

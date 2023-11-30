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
            var propertyLocations = GetPositionalFields(identity.Template);
            if (propertyLocations.Count != identity.ArgumentTypes.Length)
            {
                // The number of properties in the template doesn't match the number of arguments passed.
                return null;
            }

            int tail=0;
            string template = identity.Template;
            var messagePieces = new();
            var properties = new();
            foreach(var (start, end) in propertyLocations)
            {
                string piece = template.Substring(tail, start - tail - 1);
                string prop = template.Substring(start, end - start);
                
                (string name, string format) = ParseProperty(prop);
                
                properties.Add((name, identity.ArgumentTypes[properties.Length], format));
                
                messagePieces.Add((piece, properties.Length - 1));
                tail = end + 1;
            }
            
            if (tail < template.Length)
            {
              // A piece of text is left at the end and there is no property after.
              MessagePieces.Add((template.Substring(tail, template.Length - tail), -1));
            }

            return new LogMethodDetails(identity.Level, identity.Name, identity.Template, properties, messagePieces);
        }

        // Start is first letter, end is  curly bracket.
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
        
        private static (string name, string format) ParseProperty(string prop)
        {
          int semicolonIndex = prop.IndexOf(':');
          
          if (semicolonIndex == -1)
          {
            return (prop, string.Empty);
          }
          
          return (
            prop.Substring(0, semicolonIndex),
            prop.Substring(semicolonIndex + 1, prop.Length - semicolonIndex - 1)
            );
        }
    }
}

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
        public List<string> MessagePieces { get; set; }

        public LogMethodDetails(
            LogLevel level,
            string name,
            string template,
            List<(string name, ITypeSymbol type, string format)> properties,
            List<string> messagePieces)
        {
            Level = level;
            Name = name;
            Template = template;
            Properties = properties;
            MessagePieces = messagePieces;
        }

        public int CalculateEventId()
        {
            int hashCode = -1685887027;
            hashCode = hashCode * -1521134295 + Level.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
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
            List<string> messagePieces = new();
            List<(string name, ITypeSymbol type, string format)> properties = new();
            foreach(var (start, end) in propertyLocations)
            {
                string piece = template.Substring(tail, start - tail - 1);
                string prop = template.Substring(start, end - start);
                
                (string name, string format) = ParseProperty(prop);
                
                properties.Add((name, identity.ArgumentTypes[properties.Count], format));
                
                messagePieces.Add(piece);
                tail = end + 1;
            }
            
            messagePieces.Add(template.Substring(tail, template.Length - tail));

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

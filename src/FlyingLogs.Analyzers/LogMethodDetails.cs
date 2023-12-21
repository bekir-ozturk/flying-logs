using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

using FlyingLogs.Shared;

using Microsoft.CodeAnalysis;

namespace FlyingLogs.Analyzers
{
    internal enum TypeSerializationMethod
    {
        /// <summary>
        /// Indicates that a property of this type should be converted to text format by calling ToString method on it.
        /// </summary>
        ToString,

        /// <summary>
        /// Indicates that a property of this type should be converted to text format by directly calling 
        /// IUtf8SpanFormattable.TryFormat on it. Property should not be cast to IUtf8SpanFormattable explicitly as it
        /// may cause boxing.
        /// </summary>
        ImplicitIUtf8SpanFormattable,

        /// <summary>
        /// Indicates that a property of this type should be converted to text format by first casting it to
        /// IUtf8SpanFormattable and then calling TryFormat on it.
        /// </summary>
        ExplicitIUtf8SpanFormattable,
    }

    internal class LogMethodDetails : IEquatable<LogMethodDetails>
    {
        public LogLevel Level { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public List<LogMethodProperty> Properties { get; set; }
        public List<MessagePiece> MessagePieces { get; set; }
        public string EventId { get; set; } = string.Empty;

        public LogMethodDetails(
            LogLevel level,
            string name,
            string template,
            List<LogMethodProperty> properties,
            List<MessagePiece> messagePieces)
        {
            Level = level;
            Name = name;
            Template = template;
            Properties = properties;
            MessagePieces = messagePieces;
        }

        internal static LogMethodDetails? Parse(LogLevel level, string methodName, string template, ITypeSymbol[] argumentTypes)
        {
            var propertyLocations = GetPositionalFields(template);
            if (propertyLocations.Count != argumentTypes.Length)
            {
                // The number of properties in the template doesn't match the number of arguments passed.
                return null;
            }

            int tail = 0;
            List<MessagePiece> messagePieces = new();
            List<LogMethodProperty> properties = new();
            foreach (var (start, end) in propertyLocations)
            {
                string piece = template.Substring(tail, start - tail - 1);
                string prop = template.Substring(start, end - start);

                (string name, string? format) = ParseProperty(prop);
                TypeSerializationMethod serializationMethod = TypeSerializationMethod.ToString;
                if (argumentTypes[properties.Count].AllInterfaces.Any(i => i.Name == "IUtf8SpanFormattable" && i.ContainingNamespace.Name == "System"))
                {
                    // TODO pick explicit implementation when necessary.
                    serializationMethod = TypeSerializationMethod.ImplicitIUtf8SpanFormattable;
                }

                properties.Add(new LogMethodProperty(
                    name,
                    argumentTypes[properties.Count].ToDisplayString(),
                    serializationMethod,
                    format,
                    MethodBuilder.GetPropertyNameForStringLiteral(name)));

                messagePieces.Add(new MessagePiece(piece, MethodBuilder.GetPropertyNameForStringLiteral(piece)));
                tail = end + 1;
            }

            string lastPiece = template.Substring(tail, template.Length - tail);
            messagePieces.Add(new MessagePiece(lastPiece, MethodBuilder.GetPropertyNameForStringLiteral(lastPiece)));

            return new LogMethodDetails(level, methodName, template, properties, messagePieces);
        }

        internal LogMethodDetails CreateJsonEscapedClone(
            out bool templateChanged,
            out bool propertyNamesChanged,
            out bool piecesChanged)
        {
            var result = new LogMethodDetails(Level, Name, Template, Properties, MessagePieces);

            result.Template = JavaScriptEncoder.Default.Encode(Template);
            templateChanged = result.Template != Template;

            propertyNamesChanged = false;
            for (int i=0; i < Properties.Count; i++)
            {
                string escapedPropertyName = JavaScriptEncoder.Default.Encode(Properties[i].Name);
                if (escapedPropertyName == Properties[i].Name)
                {
                    if (!propertyNamesChanged)
                        continue; // All string were the same so far. Continue iterating the properties.
                    else
                    {
                        // A copy of the list is being filled. Even though this property doesn't need to change,
                        // it needs to exist in the new list.
                        result.Properties.Add(Properties[i]);
                    }
                }
                else
                {
                    if (!propertyNamesChanged)
                    {
                        // Everything has been the same so far, but not anymore. Duplicate the list.
                        result.Properties = new List<LogMethodProperty>(Properties.Take(i));
                        propertyNamesChanged = true;
                    }

                    result.Properties.Add(Properties[i] with 
                    {
                        Name = escapedPropertyName,
                        EncodedConstantPropertyName = MethodBuilder.GetPropertyNameForStringLiteral(escapedPropertyName)
                    });
                }
            }

            piecesChanged = false;
            for (int i=0; i < MessagePieces.Count; i++)
            {
                string escapedPiece = JavaScriptEncoder.Default.Encode(MessagePieces[i].Value);
                if (escapedPiece == MessagePieces[i].Value)
                {
                    if (!piecesChanged)
                        continue; // All string were the same so far. Continue iterating the properties.
                    else
                    {
                        // A copy of the list is being filled. Even though this property doesn't need to change,
                        // it needs to exist in the new list.
                        result.MessagePieces.Add(MessagePieces[i]);
                    }
                }
                else
                {
                    if (!piecesChanged)
                    {
                        // Everything has been the same so far, but not anymore. Duplicate the list.
                        result.MessagePieces = new List<MessagePiece>(MessagePieces.Take(i));
                        piecesChanged = true;
                    }

                    result.MessagePieces.Add(MessagePieces[i] with
                    {
                        Value = escapedPiece,
                        EncodedConstantPropertyName = MethodBuilder.GetPropertyNameForStringLiteral(escapedPiece)
                    });
                }
            }

            return result;
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

        private static (string name, string? format) ParseProperty(string prop)
        {
            int semicolonIndex = prop.IndexOf(':');

            if (semicolonIndex == -1)
            {
                return (prop, null);
            }

            return (
              prop.Substring(0, semicolonIndex),
              prop.Substring(semicolonIndex + 1, prop.Length - semicolonIndex - 1)
              );
        }

        public override bool Equals(object? obj)
        {
            return obj is LogMethodDetails details && this == details;
        }

        public override int GetHashCode()
        {
            int hashCode = 988620377;
            hashCode = hashCode * -1521134295 + Level.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Template);
            hashCode = hashCode * -1521134295 + EventId.GetHashCode();

            if (Properties != null)
            {
                foreach (var property in Properties)
                {
                    hashCode = hashCode * -1521134295 + property.GetHashCode();
                }
            }

            if (MessagePieces != null)
            {
                foreach (var piece in MessagePieces)
                {
                    hashCode = hashCode * -1521134295 + piece.GetHashCode();
                }
            }
            return hashCode;
        }

        public bool Equals(LogMethodDetails other)
        {
            return this == other;
        }

        public static bool operator ==(LogMethodDetails? left, LogMethodDetails? right)
        {
            if (object.Equals(left, null) && object.Equals(right, null)) return true;
            if (object.Equals(left, null) || object.Equals(right, null)) return false;
            
            return left.Level == right.Level &&
                   left.Name == right.Name &&
                   left.Template == right.Template &&
                   left.EventId == right.EventId &&
                   (left.Properties == right.Properties
                        || (left.Properties?.SequenceEqual(right.Properties) ?? false)) &&
                   (left.MessagePieces == right.MessagePieces
                        || (left.MessagePieces?.SequenceEqual(right.MessagePieces) ?? false));
        }

        public static bool operator !=(LogMethodDetails? left, LogMethodDetails? right)
        {
            return !(left == right);
        }
    }

    internal record struct LogMethodProperty(
        string Name,
        string TypeName,
        TypeSerializationMethod TypeSerialization,
        string? Format,
        string EncodedConstantPropertyName);

    internal record struct MessagePiece(
        string Value,
        string EncodedConstantPropertyName);
}

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
        /// Indicates that the property is already of type 'string' and requires no call to 'ToString()'.
        /// </summary>
        None,

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

        /// <summary>
        /// Indicates that this property is a complex object and its fields will be serialized separately in the
        /// following properties. Therefore, 'ToString' or 'TryFormat' is not called on this object. We just set the
        /// value to a <see cref="PropertyValueHints"/> and pass it to the sink.
        /// </summary>
        Complex,

        /// <summary>
        /// Indicates that the property type is 'bool' and it should be serialized accordingly.
        /// </summary>
        Bool,

        /// <summary>
        /// Indicates that the property type is 'System.DateTime' and it should be serialized accordingly.
        /// </summary>
        DateTime,
    }

    internal enum LogMethodUsageError
    {
        None = 0,
        NameNotUnique = 1,
    }

    internal class LogMethodDetails : IEquatable<LogMethodDetails>
    {
        public LogLevel Level { get; set; }
        public string Name { get; set; }
        public string Template { get; set; }
        public List<LogMethodProperty> Properties { get; set; }
        public List<MessagePiece> MessagePieces { get; set; }
        public string EventId { get; set; } = string.Empty;
        public LogMethodUsageError MethodUsageError { get; set; } = LogMethodUsageError.None;

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
            int tail = 0;
            int rootPropertyCount = 0;
            List<MessagePiece> messagePieces = new();
            List<LogMethodProperty> properties = new();

            var propertyLocations = GetPositionalFields(template);
            foreach (var (start, end) in propertyLocations)
            {
                string piece = template.Substring(tail, start - tail - 1);
                string prop = template.Substring(start, end - start);

                (string name, string? format) = ParseProperty(prop);

                bool isComplex = false;
                if (name.StartsWith("@"))
                {
                    isComplex = true;
                    name = name.Substring(1);
                }

                ITypeSymbol? argumentType = argumentTypes.Length > rootPropertyCount
                    ? argumentTypes[rootPropertyCount]
                    : null;
                TypeSerializationMethod serializationMethod = GetSerializationMethodForType(argumentType, isComplex);
                
                rootPropertyCount++;
                properties.Add(new LogMethodProperty(
                    Depth: 0,
                    Name: name,
                    TypeName: argumentType?.ToDisplayString() ?? "System.Object",
                    TypeSerialization: serializationMethod,
                    Format: format,
                    EncodedConstantPropertyName: MethodBuilder.GetPropertyNameForStringLiteral(name),
                    IsNullable: argumentType == null
                        || argumentType.IsReferenceType 
                        || (argumentType.TypeKind == TypeKind.Struct
                            && argumentType.NullableAnnotation == NullableAnnotation.Annotated
                        ),
                    PropertyAccessorPrefix: string.Empty
                ));

                if (serializationMethod == TypeSerializationMethod.Complex && argumentType != null)
                    ExpandComplexObject(argumentType!, properties, 0, 2);

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
            
            result.EventId = EventId;
            result.Template = JavaScriptEncoder.Default.Encode(Template);
            templateChanged = result.Template != Template;

            propertyNamesChanged = false;
            for (int i = 0; i < Properties.Count; i++)
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
            for (int i = 0; i < MessagePieces.Count; i++)
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

        // Start is first letter, end is curly bracket.
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

        private static void ExpandComplexObject(
            ITypeSymbol type,
            List<LogMethodProperty> targetList,
            int currentDepth,
            int maxDepth)
        {
            var members = type.GetMembers();
            string propertyAccessorPrefix = string.Empty;

            if (type.TypeKind == TypeKind.Struct && type.NullableAnnotation == NullableAnnotation.Annotated)
            {
                // Nullable object. Skip the 'HasValue' and 'Value' Properties and start serializing from the value.
                var valueProperty = type.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();
                // Any nullable struct should have a 'Value' property, but let's check just in case.
                if (valueProperty != null)
                {
                    // Use the members of the 'Value' and not the Nullable<TValue>.
                    members = valueProperty.Type.GetMembers();
                    propertyAccessorPrefix = "Value.";
                }
            }

            for (int i = 0; i < members.Length; i++)
            {
                string name;
                ITypeSymbol memberType;
                string typeName;

                ISymbol member = members[i];
                if (member is IFieldSymbol field)
                {
                    if (field.DeclaredAccessibility != Accessibility.Public
                        || field.IsStatic || field.IsConst)
                        continue;

                    name = field.Name;
                    memberType = field.Type;
                    typeName = field.Type.ToDisplayString();
                }
                else if (member is IPropertySymbol property)
                {
                    if (property.DeclaredAccessibility != Accessibility.Public
                        || property.IsStatic || property.IsAbstract || property.IsWriteOnly
                        || !property.CanBeReferencedByName || property.GetMethod == null)
                        continue;

                    name = property.Name;
                    memberType = property.Type;
                    typeName = property.Type.ToDisplayString();
                }
                else
                    continue;

                var serializationMethod = GetSerializationMethodForType(memberType, currentDepth < maxDepth);

                targetList.Add(new LogMethodProperty(
                    Depth: currentDepth + 1,
                    Name: name,
                    TypeName: typeName,
                    TypeSerialization: serializationMethod,
                    Format: null,
                    EncodedConstantPropertyName: MethodBuilder.GetPropertyNameForStringLiteral(name),
                    IsNullable: memberType.IsReferenceType 
                        || (memberType.TypeKind == TypeKind.Struct
                            && memberType.NullableAnnotation == NullableAnnotation.Annotated
                        ),
                    PropertyAccessorPrefix: propertyAccessorPrefix
                ));

                if (serializationMethod == TypeSerializationMethod.Complex)
                    ExpandComplexObject(memberType, targetList, currentDepth + 1, maxDepth);
            }
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

        private static TypeSerializationMethod GetSerializationMethodForType(ITypeSymbol? argumentType, bool complexObjectExpansionAllowed)
        {
            if (argumentType == null)
            {
                // User hasn't provided this argument yet. Assume 'object'.
                // Fallback to 'ToString' since any 'object' supports it.
            }
            else if (argumentType.SpecialType == SpecialType.System_String)
            {
                return TypeSerializationMethod.None;
            }
            else if (argumentType.SpecialType == SpecialType.System_Boolean)
            {
                return TypeSerializationMethod.Bool;
            }
            else if (argumentType.SpecialType == SpecialType.System_DateTime)
            {
                return TypeSerializationMethod.DateTime;
            }
            else if (complexObjectExpansionAllowed &&
                ( argumentType.TypeKind == TypeKind.Class 
                || argumentType.TypeKind == TypeKind.Interface
                || argumentType.TypeKind == TypeKind.Struct)
                && ( argumentType.SpecialType == SpecialType.None
                || argumentType.SpecialType == SpecialType.System_Object))
            {
                return TypeSerializationMethod.Complex;
            }
            else
            {
                var utf8FormattableImplementation = argumentType.AllInterfaces.FirstOrDefault(
                    i => i.Name == "IUtf8SpanFormattable" && i.ContainingNamespace.Name == "System");

                if (utf8FormattableImplementation != null)
                {
                    // Try format shouldn't be null unless it is removed from the interface, but lets not fail if it is.
                    var tryFormatMethod = utf8FormattableImplementation.GetMembers("TryFormat").FirstOrDefault();

                    return tryFormatMethod != null
                        && argumentType.FindImplementationForInterfaceMember(tryFormatMethod) is IMethodSymbol methodSymbol
                        && methodSymbol.ExplicitInterfaceImplementations.Contains(tryFormatMethod, SymbolEqualityComparer.Default)
                        ? TypeSerializationMethod.ExplicitIUtf8SpanFormattable
                        : TypeSerializationMethod.ImplicitIUtf8SpanFormattable;
                }
            }

            return TypeSerializationMethod.ToString;
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
        int Depth,
        string Name,
        string TypeName,
        TypeSerializationMethod TypeSerialization,
        string? Format,
        string EncodedConstantPropertyName,
        bool IsNullable,
        /// Stores the prefix we use when accessing this property at runtime.
        /// This is used to bring deep level fields onto higher levels in the output.
        /// For instance, all nullable structs are behind a 'Value' property today. To access a property of type Point?,
        /// the code should be 'point.Value.X' instead of just 'point.X'. To avoid having this extra 'Value' in the log
        /// we skip the members of Nullable<T> and only include the members of Value in the results. This makes source
        /// generator attempt to access the property using 'point.X' which results in a compiler error since X is not
        /// directly a member of Point?. We use this prefix to hint to the source generator that the property is behind
        /// another property so that it can instead access it correctly.
        string PropertyAccessorPrefix);

    internal record struct MessagePiece(
        string Value,
        string EncodedConstantPropertyName);
}

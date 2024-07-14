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

    internal record LogMethodDetails(
        LogLevel Level,
        string Name,
        string Template,
        List<LogMethodProperty> Properties,
        List<MessagePiece> MessagePieces,
        FileLinePositionSpan InvocationLocation)
    {
        public string EventId { get; set; } = string.Empty;
        public DiagnosticDescriptor? Diagnostic { get; set; } = null;
        public string? DiagnosticArgument { get; set; } = null;

        internal static LogMethodDetails Parse(
            LogLevel level,
            string methodName,
            string template,
            (string? name, ITypeSymbol type)[] argumentTypes,
            FileLinePositionSpan invocationLocation)
        {
            int tail = 0;
            List<MessagePiece> messagePieces = new();
            List<LogMethodProperty> properties = new();

            var propertyLocations = GetPositionalFields(template);
            for (int i = 0; i < propertyLocations.Count; i++)
            {
                (int start, int end) = propertyLocations[i];
                string piece = template.Substring(tail, start - tail - 1);
                string prop = template.Substring(start, end - start);

                (string nameFromTemplate, string? format) = ParseProperty(prop);
                (string? name, ITypeSymbol type) = argumentTypes.Length > i ? argumentTypes[i] : default;

                name = name ?? nameFromTemplate ?? "";
                bool expand = false;
                if (name.StartsWith("@"))
                {
                    expand = true;
                    name = name.Substring(1);
                }

                ExpandComplexObject(name, format, type, expand, properties, 0, 2);

                messagePieces.Add(new MessagePiece(piece, MethodBuilder.GetPropertyNameForStringLiteral(piece)));
                tail = end + 1;
            }

            string lastPiece = template.Substring(tail, template.Length - tail);
            messagePieces.Add(new MessagePiece(lastPiece, MethodBuilder.GetPropertyNameForStringLiteral(lastPiece)));

            for (int i=propertyLocations.Count; i < argumentTypes.Length; i++)
            {
                bool expand = false;
                (string? name, ITypeSymbol type) = argumentTypes.Length > i ? argumentTypes[i] : default;
                name = name ?? "";

                if (name.StartsWith("@"))
                {
                    expand = true;
                    name = name.Substring(1);
                }

                ExpandComplexObject(name, null, type, expand, properties, 0, 2);
            }

            return new LogMethodDetails(level, methodName, template, properties, messagePieces, invocationLocation);
        }

        internal LogMethodDetails CreateJsonEscapedClone(
            out bool templateChanged,
            out bool propertyNamesChanged,
            out bool piecesChanged)
        {
            var result = new LogMethodDetails(Level, Name, Template, Properties, MessagePieces, InvocationLocation)
            {
                EventId = EventId,
                Template = JavaScriptEncoder.Default.Encode(Template)
            };
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
                        result = result with{
                            Properties = new List<LogMethodProperty>(Properties.Take(i))
                        };
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
                        result = result with
                        {
                            MessagePieces = new List<MessagePiece>(MessagePieces.Take(i))
                        };

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
            string name,
            string? format,
            ITypeSymbol type,
            bool expansionRequested,
            List<LogMethodProperty> targetList,
            int currentDepth,
            int maxDepth)
        {
            string propertyAccessorPostfix = string.Empty;
            ITypeSymbol? virtualType = null;

            if (type?.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T)
            {
                // Nullable object. Skip the 'HasValue' and 'Value' Properties and start serializing from the value.
                var valueProperty = type.GetMembers("Value").OfType<IPropertySymbol>().FirstOrDefault();
                // Any nullable struct should have a 'Value' property, but let's check just in case.
                if (valueProperty != null)
                {
                    // Use the type of the 'Value' and not 'Nullable<TValue>'.
                    virtualType = valueProperty.Type;
                    // Since we skipped the 'Value', we should tell the source generator that to access the property
                    // it needs to prefix it with 'Value.'.
                    propertyAccessorPostfix = ".Value";
                }
            }

            expansionRequested &= currentDepth < maxDepth;
            var serializationMethod = GetSerializationMethodForType(virtualType ?? type, expansionRequested);

            targetList.Add(new LogMethodProperty(
                Depth: currentDepth,
                Name: name,
                TypeName: type?.ToDisplayString() ?? "System.Object",
                TypeNameWithoutNullableAnnotation: type?.ToDisplayString(NullableFlowState.None) ?? "System.Object",
                TypeSerialization: serializationMethod,
                Format: format,
                EncodedConstantPropertyName: MethodBuilder.GetPropertyNameForStringLiteral(name),
                IsNullable: type == null
                    || type.IsReferenceType
                    || type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T,
                PropertyAccessorPostfix: propertyAccessorPostfix
            ));

            if (serializationMethod != TypeSerializationMethod.Complex || type == null)
                return;

            var members = (virtualType ?? type).GetMembers();

            for (int i = 0; i < members.Length; i++)
            {
                string childName;
                ITypeSymbol memberType;

                ISymbol member = members[i];
                if (member is IFieldSymbol field)
                {
                    if (field.DeclaredAccessibility != Accessibility.Public
                        || field.IsStatic || field.IsConst)
                        continue;

                    childName = field.Name;
                    memberType = field.Type;
                }
                else if (member is IPropertySymbol property)
                {
                    if (property.DeclaredAccessibility != Accessibility.Public
                        || property.IsStatic || property.IsAbstract || property.IsWriteOnly
                        || !property.CanBeReferencedByName || property.GetMethod == null)
                        continue;

                    childName = property.Name;
                    memberType = property.Type;
                }
                else
                    continue;

                ExpandComplexObject(
                    childName,
                    format: null,
                    memberType,
                    expansionRequested: true,
                    targetList,
                    currentDepth + 1,
                    maxDepth);
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
                (argumentType.TypeKind == TypeKind.Class
                || argumentType.TypeKind == TypeKind.Interface
                || argumentType.TypeKind == TypeKind.Struct)
                && (argumentType.SpecialType == SpecialType.None
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
    }

    internal record struct LogMethodProperty(
        int Depth,
        string Name,
        string TypeName,
        string TypeNameWithoutNullableAnnotation,
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
        string PropertyAccessorPostfix);

    internal record struct MessagePiece(
        string Value,
        string EncodedConstantPropertyName);
}

using System.Collections.Immutable;

namespace FlyingLogs.Core
{
    /// <summary>
    /// Represents all the data of a log event.
    /// </summary>
    public class RawLog
    {
        /// <summary>
        /// List of all the properties included in the log. Each item is a tuple where the first element is the name of
        /// the property encoded in Utf8. The second item is the value of the property as string, encoded in Utf8.
        /// This list is meant to be indexed by <see cref="LogProperty"/> enum. Within the list, after the properties 
        /// declared in <see cref="LogProperty"/> comes the custom properties. Index of the first custom property is
        /// <see cref="LogProperty.CUSTOM_PROPERTIES_START"/>.
        /// </summary>
        public readonly List<(ReadOnlyMemory<byte>, ReadOnlyMemory<byte>)> Properties = new(16);

        /// <summary>
        /// Represents all the text pieces that make up the rendered message. A rendered message is the message
        /// template where all the properties are replaced with their formatted values. Each item in this array
        /// consists of two elements; first element is a piece of string encoded in Utf8. The second element is the
        /// index to the property stored in <see cref="Properties"/> that should be rendered right after the string.
        /// Property index can be <code>(LogProperty)-1</code> for the last item in the list in which case it should be
        /// skipped.
        /// </summary>
        /// <example>
        /// For template <code>"Player health was reduced by {damage} to value {final_health}.</code> the array
        /// contains the following values:
        /// <list type="number">
        /// <item>
        ///     <code>("Player health was reduced by "u8, LogProperty.CUSTOM_PROPERTIES_START)</code>
        /// </item>
        /// <item>
        ///     <code>(" to value "u8, LogProperty.CUSTOM_PROPERTIES_START + 1)</code>
        /// </item>
        /// <item>
        ///     <code>("."u8, (LogProperty)-1)</code>
        /// </item>
        /// </list>
        /// </example>
        public ImmutableArray<(Memory<byte>, LogProperty)> MessageSlices = ImmutableArray<(Memory<byte>, LogProperty)>.Empty;
    }
}

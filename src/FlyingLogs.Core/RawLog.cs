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
        /// declared in <see cref="LogProperty"/> comes the positional and custom properties. Index of the first
        /// positional property (that is a property that is referenced in the message template) is
        /// <see cref="PositionalPropertiesStartIndex"/>. Additional properties, which are explictly passed to the log
        /// method after the positional properties but are not referenced in the templates, are included in the list
        /// starting with <see cref="AdditionalPropertiesStartIndex"/>.
        /// </summary>
        public readonly List<(ReadOnlyMemory<byte>, ReadOnlyMemory<byte>)> Properties = new(16);

        /// Index in the <see cref="Properties"/> list where the properties in the message template begin.
        public int PositionalPropertiesStartIndex;
        
        /// Index in the <see cref="Properties"/> list where the additional properties that are not mapped in the 
        /// message template begin.
        public int AdditionalPropertiesStartIndex;
        
        /// <summary>
        /// Represents all the text pieces that make up the rendered message except for the value of the positional
        /// properties which are determined at runtime. Between each of these slices, there is always a positional
        /// property. If your message template contains n positional properties, length of this array is n+1.
        /// </summary>
        public ImmutableArray<ReadOnlyMemory<byte>> MessageSlices = ImmutableArray<ReadOnlyMemory<byte>>.Empty;
    }
}

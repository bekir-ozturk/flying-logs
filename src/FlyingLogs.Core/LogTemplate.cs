using FlyingLogs.Shared;

namespace FlyingLogs.Core
{
    /// <summary>
    /// Represents all the data of a log event.
    /// </summary>
    public record LogTemplate
    (
        /// <summary>
        /// Severity of this event.
        /// </summary>
         LogLevel Level,

        /// <summary>
        /// Identifier string for this template encoded as specified in <see cref="Encoding"/>. 
        /// </summary>
        ReadOnlyMemory<byte> EventId,

        /// <summary>
        /// Log message, same as it was provided in the log call, without any properties formatted into it.
        /// </summary>
        ReadOnlyMemory<byte> TemplateString,

        /// <summary>
        /// Represents all the text pieces that make up the rendered message except for the value of the positional
        /// properties which are determined at runtime. Between each of these pieces, there is always a positional
        /// property. If your message template contains n positional properties, length of this array is n+1.
        /// </summary>
        ReadOnlyMemory<ReadOnlyMemory<byte>> MessagePieces,

        /// <summary>
        /// Names of the properties.
        /// </summary>
        ReadOnlyMemory<ReadOnlyMemory<byte>> PropertyNames,

        /// <summary>
        /// Types of the properties. The length is equal to <see cref="PropertyNames"/>.
        /// </summary>
        ReadOnlyMemory<Type> PropertyTypes,

        /// <summary>
        /// Depths of properties. All positional properties have depth of zero. Additional properties and assembly level
        /// properties also have zero depth. If any of the level-zero properties are complex objects, their immediate
        /// fields will have depth value of one. If they are also complex objects, their fields will have a depth of 2.
        /// For complex objects, child field traversal is done in a depth-first manner.  The length is always equal to
        /// <see cref="PropertyNames"/>.
        /// </summary>
        ReadOnlyMemory<byte> PropertyDepths
    ){ }
}

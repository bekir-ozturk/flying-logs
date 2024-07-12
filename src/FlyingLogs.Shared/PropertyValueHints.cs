using System;

namespace FlyingLogs.Shared;

public static class PropertyValueHints
{
    /// <summary>
    /// To create new hints, slice unique parts of this array into a ReadOnlyMemory.
    /// ReadOnlyMemory/Memory evaluate equality based on whether the two instances refer to the same region in memory.
    /// This helps us easily decide whether the 1 byte we just received is the actual value of the property or it is a
    /// hint with a special meaning.
    /// </summary>
    private static readonly ReadOnlyMemory<byte> BackingMemory = new byte[4];

    /// <summary>
    /// Indicates that the value of the property was null.
    /// Sinks decide whether they want to explicitly specify the null properties or skip them.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> Null = BackingMemory.Slice(0,0);

    /// <summary>
    /// Indicates that no value was passed to the log method for this property; not even 'null'.
    /// An example use is: when a complex object has the value 'null', then its fields will have this hint.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> Skip = BackingMemory.Slice(1,0);

    /// <summary>
    /// Indicates that the object is a complex object and its fields will be provided in the following properties.
    /// Consecutive properties that have a higher depth than this property are all part of this object.
    /// If property list ends with this object or the next property has the same or a lower depth, then this complex
    /// object has no fields and it is a simple empty object.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> Complex = BackingMemory.Slice(2,0);

    /// <summary>
    /// Indicates that the object is a complex object (<see cref="Complex">), but the value is null.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> ComplexNull = BackingMemory.Slice(3,0);
}
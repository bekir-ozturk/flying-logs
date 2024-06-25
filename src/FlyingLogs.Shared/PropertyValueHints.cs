using System;

public static class PropertyValueHints
{
    /// <summary>
    /// To create new hints, slice unique parts of this array into a ReadOnlyMemory.
    /// ReadOnlyMemory/Memory evaluate equality based on whether the two instances refer to the same region in memory.
    /// This helps us easily decide whether the 1 byte we just received is the actual value of the property or it is a
    /// hint with a special meaning.
    /// </summary>
    private static readonly ReadOnlyMemory<byte> BackingMemory = new byte[2];

    /// <summary>
    /// Indicates that the value of the property was null.
    /// Sinks decide whether they want to explicitly specify the null properties or skip them.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> Null = BackingMemory.Slice(0,1);

    /// <summary>
    /// Indicates that no value was passed to the log method for this property; not even 'null'.
    /// An example use is: when a complex object has the value 'null', then its fields will have this hint.
    /// </summary>
    public static readonly ReadOnlyMemory<byte> Skip = BackingMemory.Slice(1,1);
}
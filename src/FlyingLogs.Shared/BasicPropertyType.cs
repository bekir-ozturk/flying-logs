namespace FlyingLogs.Shared
{
    /// <summary>
    /// Represent the type of a property after it was serialized to UTF8.
    /// This is to allow sinks to customize the output based on the type of the property.
    /// </summary>
    public enum BasicPropertyType : byte
    {
        /// <summary>
        /// Indicates that the property is a string. Any complex object (structs, classes, records etc.) will be
        /// represented with this type.
        /// </summary>
        String = 0,

        /// <summary>
        /// Indicates that the property is an integral numeric type (int, byte, char, ulong etc.).
        /// </summary>
        Integer = 1,

        /// <summary>
        /// Indicates that the property is a floating point number (float, double or decimal).
        /// </summary>
        Fraction = 2,

        /// <summary>
        /// Indicates that the property is a bool.
        /// </summary>
        Bool = 3,

        /// <summary>
        /// Indicates that the property is a System.DateTime instance.
        /// </summary>
        DateTime = 4,
    }
}

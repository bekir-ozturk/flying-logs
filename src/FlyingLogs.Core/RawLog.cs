namespace FlyingLogs.Core
{
    /// <summary>
    /// Represents all the data of a log event.
    /// </summary>
    public class RawLog
    {
        public readonly ReadOnlyMemory<byte>[] BuiltinProperties;
        /// <summary>
        /// List of all the properties included in the log. Each item is a tuple where the first element is the name of
        /// the property encoded in Utf8. The second item is the value of the property as string, encoded in Utf8.
        /// The first properties in this list are positional properties: properties that were named in the template.
        /// There are exactly <code>MessagePieces.Count - 1</code> positional properties. Afterwards come the
        /// additional properties which are either given as an argument to the log method after the positional
        /// properties or are added by enrichers.
        /// </summary>
        public readonly List<(ReadOnlyMemory<byte> name, ReadOnlyMemory<byte> value)> Properties = new(16);

        /// <summary>
        /// Represents all the text pieces that make up the rendered message except for the value of the positional
        /// properties which are determined at runtime. Between each of these pieces, there is always a positional
        /// property. If your message template contains n positional properties, length of this array is n+1.
        /// </summary>
        public ReadOnlyMemory<ReadOnlyMemory<byte>> MessagePieces = ReadOnlyMemory<ReadOnlyMemory<byte>>.Empty;

        public RawLog()
        {
            int builtinPropertyCount = Enum.GetValues<BuiltInProperty>().Length;
            BuiltinProperties = new ReadOnlyMemory<byte>[builtinPropertyCount];
        }

        /// <summary>
        /// Cleans up any old data from the instance and makes it ready to store the details of another log event.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < BuiltinProperties.Length; i++)
            {
                BuiltinProperties[i] = Memory<byte>.Empty;
            }

            Properties.Clear();

            MessagePieces = ReadOnlyMemory<ReadOnlyMemory<byte>>.Empty;
        }
    }
}

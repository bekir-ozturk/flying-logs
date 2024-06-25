using System;
using System.Collections.Generic;

namespace FlyingLogs.Shared;

public static class JsonUtilities
{
    public static bool JsonEncodePropertyValues(
        IReadOnlyList<ReadOnlyMemory<byte>> values,
        Memory<byte> buffer,
        List<ReadOnlyMemory<byte>> results)
    {
        int valueCount = values.Count;
        for (int i=0; i<valueCount; i++)
        {
            
        }
    }
}

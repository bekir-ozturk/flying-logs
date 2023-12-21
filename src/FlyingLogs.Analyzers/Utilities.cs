using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FlyingLogs.Analyzers
{
    internal static class Utilities
    {
        internal static int CalculateAssemblyNameHash(string value)
        {
            using (var md5 = MD5.Create())
            {
                int nameBytesLength = Encoding.Unicode.GetByteCount(value);
                byte[] nameBytes = ArrayPool<byte>.Shared.Rent(nameBytesLength);
                int usedBytes = Encoding.Unicode.GetBytes(value, 0, value.Length, nameBytes, 0);

                byte[] hash = md5.ComputeHash(nameBytes, 0, usedBytes);
                ArrayPool<byte>.Shared.Return(hash);
                return MemoryMarshal.Cast<byte, int>(hash.AsSpan())[0];
            }
        }
    }
}

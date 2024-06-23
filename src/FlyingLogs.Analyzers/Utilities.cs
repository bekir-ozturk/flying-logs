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

        // Source: https://andrewlock.net/why-is-string-gethashcode-different-each-time-i-run-my-program-in-net-core/
        internal static int GetDeterministicHashCode(this string str)
        {
            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}

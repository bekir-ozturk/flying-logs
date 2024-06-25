using System.Text;

using FlyingLogs.Core;

namespace FlyingLogs.UseCaseTests
{
    internal static class RawLogExtensionMethods
    {
        public static string PieceAsString(this LogTemplate log, int index)
        {
            return Encoding.UTF8.GetString(log.MessagePieces.Span[index].Span);
        }

        public static string PropertyNameAsString(this LogTemplate log, int index)
        {
            return Encoding.UTF8.GetString(log.PropertyNames.Span[index].Span);
        }

        public static string PropertyValueAsString(this IReadOnlyList<ReadOnlyMemory<byte>> values, int index)
        {
            return Encoding.UTF8.GetString(values[index].Span);
        }
    }
}

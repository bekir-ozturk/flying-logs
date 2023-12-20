using System.Text;

using FlyingLogs.Core;

namespace FlyingLogs.UseCaseTests
{
    internal static class RawLogExtensionMethods
    {
        public static string PieceAsString(this RawLog log, int index)
        {
            return Encoding.UTF8.GetString(log.MessagePieces.Span[index].Span);
        }

        public static string PropertyNameAsString(this RawLog log, int index)
        {
            return Encoding.UTF8.GetString(log.Properties[index].name.Span);
        }

        public static string PropertyValueAsString(this RawLog log, int index)
        {
            return Encoding.UTF8.GetString(log.Properties[index].value.Span);
        }
    }
}

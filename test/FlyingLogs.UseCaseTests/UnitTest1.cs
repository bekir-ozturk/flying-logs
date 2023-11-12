using System.Numerics;
using System.Text;

using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            FlyingLogs.Log.Error.LogThisMf("messsage");
            Log.Information.L1("whatever{position} {speed} and some {duration}", 123.2f, Vector2.One, 1.3f);
            Assert.Pass();
        }

        [Test] public void Test2()
        {
            LoggerThread logger = new LoggerThread();

            int attempt = 0;
            while (true)
            {
                Console.ReadKey();
                (int left, int top) = Console.GetCursorPosition();
                Console.SetCursorPosition(0, top);
                Console.WriteLine("Sending #" + ++attempt);
                string msg = $@"{{""@t"":""{DateTime.UtcNow.ToString("s") + "Z"}"",""@mt"":""mesaaaj {{sequenceNumber}}"",""sequenceNumber"":""{attempt}""}}";
                Memory<byte> targetBuffer = logger.Buffer.PeekWrite(msg.Length);
                if (targetBuffer.Length == 0)
                    targetBuffer = targetBuffer;
                else
                {
                    bool encodeResult = Encoding.ASCII.TryGetBytes(msg, targetBuffer.Span, out int byteCount);
                    if ( !encodeResult )
                    {
                        encodeResult = encodeResult;
                    }
                    logger.Buffer.Push(byteCount);
                }
            }
        }
    }
}
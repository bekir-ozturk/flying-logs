using System.Numerics;
using System.Text;

using FlyingLogs.Core;
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
            FlyingLogs.Configuration.Initialize(new CustomSink());
            FlyingLogs.Log.Error.LogThis("messsage");
            FlyingLogs.Log.Trace.LogThis("messsage");
            Log.Information.L1("whatever{position} {speed} and some {duration}", 123.2f, Vector2.One, 1.3f);
            Assert.Pass();
            FlyingLogs.Shared.LogLevel logLevel = Shared.LogLevel.Critical;
        }

        private class CustomSink : ISink
        {
            public void Ingest(RawLog log)
            {
                log = log;
            }

            public bool IsLogLevelActive(LogLevel level)
            {
                return level != LogLevel.Trace;
            }
        }
        /*
        [Test] public void Test2()
        {
            SeqHttpSync logger = new SeqHttpSync();

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
*/
    }
}
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{

    [TestFixture(LogEncodings.Utf8Plain)]
    [TestFixture(LogEncodings.Utf8Json)]
    internal class LevelFilteringTests
    {
        private readonly TestSink _sink;

        public LevelFilteringTests(LogEncodings sinkExpectedEncoding)
        {
            _sink = new TestSink(sinkExpectedEncoding);
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Configuration.Initialize((LogLevel.Trace, _sink)); ;
        }

        public void CanDetermineLevelCorrectly()
        {
            LogLevel expectedLevel = LogLevel.None;
            _sink.OnIngest = log =>
                {
                    string levelName = Encoding.UTF8.GetString(log.BuiltinProperties[(int)BuiltInProperty.Level].Span);
                    Assert.That(levelName, Is.EqualTo(expectedLevel.ToString()), "Logger is outputting the wrong level.");
                };

            expectedLevel = LogLevel.Trace;
            Log.Trace.D1("This log should have level set to trace.");
            expectedLevel = LogLevel.Debug;
            Log.Debug.D2("This log should have level set to debug.");
            expectedLevel = LogLevel.Critical;
            Log.Critical.D3("This log should have level set to {level}.", "the_critical_level");
            expectedLevel = LogLevel.Warning;
            Log.Warning.D4("This log should have level set to {delta} level above {level}.", 1, LogLevel.Information);
        }

        [Test]
        public void CanFilterOutBasedOnLevel()
        {
            int ingestionTriggered = 0;

            _sink.OnIngest = log => { ingestionTriggered++; };

            Configuration.SetMinimumLogLevelForSink((_sink, LogLevel.Trace));
            Log.Trace.D5("This log should be processed");
            Assert.That(ingestionTriggered, Is.EqualTo(1));
            Configuration.SetMinimumLogLevelForSink((_sink, LogLevel.Debug));
            Log.Trace.L9("This log should be skipped.");
            Assert.That(ingestionTriggered, Is.EqualTo(1));
            Configuration.SetMinimumLogLevelForSink((_sink, LogLevel.Error));
            Log.Critical.C1("This log should be processed.");
            Assert.That(ingestionTriggered, Is.EqualTo(2));
        }
    }
}

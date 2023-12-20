using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    internal class LevelFilteringTests
    {
        private readonly TestSink _sink = new TestSink();
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Configuration.Initialize(_sink);
        }

        [Test]
        public void CanDetermineLevelCorrectly()
        {
            LogLevel expectedLevel = LogLevel.None;
            _sink.SetDelegates(
                null,
                null,
                log =>
                {
                    string levelName = Encoding.UTF8.GetString(log.BuiltinProperties[(int)BuiltInProperty.Level].Span);
                    Assert.That(levelName, Is.EqualTo(expectedLevel.ToString()), "Logger is outputting the wrong level.");
                });

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
            LogLevel minLevel = LogLevel.None;
            int ingestionTriggered = 0;

            _sink.SetDelegates(
                null,
                level => (int)level >= (int)minLevel,
                log => { ingestionTriggered++; });

            minLevel = LogLevel.Trace;
            Log.Trace.D5("This log should be processed");
            Assert.That(ingestionTriggered, Is.EqualTo(1));
            minLevel = LogLevel.Debug;
            Log.Trace.D2("This log should be skipped.");
            Assert.That(ingestionTriggered, Is.EqualTo(1));
            minLevel = LogLevel.Error;
            Log.Critical.C1("This log should be processed.");
            Assert.That(ingestionTriggered, Is.EqualTo(2));
        }
    }
}

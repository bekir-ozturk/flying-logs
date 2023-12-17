using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    internal class LevelFilteringTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void CanDetermineLevelCorrectly()
        {
            LogLevel expectedLevel = LogLevel.None;

            TestSink sink = new TestSink(
                expectedEncodingGetter: () => LogEncoding.Utf8Plain,
                level => true,
                log =>
                {
                    string levelName = Encoding.UTF8.GetString(log.BuiltinProperties[(int)BuiltInProperty.Level].Span);
                    Assert.Equals(levelName, expectedLevel.ToString());
                });

            expectedLevel = LogLevel.Trace;
            Log.Debug.D1("This log should have level set to trace.");
            expectedLevel = LogLevel.Debug;
            Log.Debug.D2("This log should have level set to debug.");
            expectedLevel = LogLevel.Critical;
            Log.Debug.D3("This log should have level set to {level}.", "the_critical_level");
        }
    }
}

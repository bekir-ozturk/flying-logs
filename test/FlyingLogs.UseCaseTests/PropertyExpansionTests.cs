using System.Globalization;
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    [TestFixture(LogEncodings.Utf8Plain)]
    [TestFixture(LogEncodings.Utf8Json)]
    internal class PropertyExpansionTests
    {
        private readonly TestSink _sink;
        
        public PropertyExpansionTests(LogEncodings sinkExpectedEncoding)
        {
            _sink = new(sinkExpectedEncoding);
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Configuration.Initialize((LogLevel.Trace, _sink));
        }

        [Test]
        public void WontExpandPrimitiveTypes()
        {
            int ingestionCount = 0;
            _sink.OnIngest = (template, values) => 
            {
                Assert.That(template.PropertyNames.Length, Is.EqualTo(1));
                Assert.That(template.PropertyDepths.Span[0], Is.EqualTo(0));
                Assert.That(template.PropertyNameAsString(0), Is.EqualTo("propertyCount"));
                ingestionCount++;
            };
            Log.Trace.T1("Here is a log with {@propertyCount} properties.", 1);
            Assert.That(ingestionCount, Is.EqualTo(1));
        }
    }
}

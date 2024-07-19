using System.Globalization;
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    internal class PropertyExpansionTests
    {
        private readonly TestSink _sink = new ();

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Configuration.Initialize(_sink);
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
            Log.Trace.P1("Here is a log with {@propertyCount} properties.", 1);
            Assert.That(ingestionCount, Is.EqualTo(1));
        }
    }
}

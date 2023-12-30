using System.Globalization;
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    [TestFixture(LogEncodings.Utf8Plain)]
    [TestFixture(LogEncodings.Utf8Json)]
    internal class PositionalPropertyTests
    {
        private readonly TestSink _sink;
        
        public PositionalPropertyTests(LogEncodings sinkExpectedEncoding)
        {
            _sink = new(sinkExpectedEncoding);
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Configuration.Initialize((LogLevel.Trace, _sink));
        }

        [Test]
        public void CanCapturePositionalProperties()
        {

            _sink.OnIngest = log => 
                    Assert.That(Encoding.UTF8.GetString(log.Properties[0].name.Span), Is.EqualTo("propertyCount"));
            Log.Trace.T1("Here is a log with {propertyCount} properties.", 1);

            _sink.OnIngest = log =>
                {
                    Assert.That(log.Properties.Count, Is.EqualTo(3));
                    Assert.That(log.PropertyNameAsString(0), Is.EqualTo("props"));
                    Assert.That(log.PropertyNameAsString(1), Is.EqualTo("log"));
                    Assert.That(log.PropertyNameAsString(2), Is.EqualTo("n"));

                    Assert.That(log.PropertyValueAsString(0), Is.EqualTo("properties"));
                    Assert.That(log.PropertyValueAsString(1), Is.EqualTo("log"));
                    Assert.That(log.PropertyValueAsString(2), Is.EqualTo("3"));
                };
            Log.Trace.T2("The number of {props} in this {log} should be {n}", "properties", "log", 3);

            _sink.OnIngest = log =>
                {
                    Assert.That(log.Properties.Count, Is.EqualTo(2));
                    Assert.That(log.PropertyNameAsString(0), Is.EqualTo("what"));
                    Assert.That(log.PropertyNameAsString(1), Is.EqualTo("thing"));

                    Assert.That(log.PropertyValueAsString(0), Is.EqualTo("A property"));
                    Assert.That(log.PropertyValueAsString(1), Is.EqualTo("message"));
                };
            Log.Trace.T3("{what} can be the first or the last thing in a {thing}", "A property", "message");

            _sink.OnIngest = log =>
                {
                    Assert.That(log.Properties.Count, Is.EqualTo(1));
                    Assert.That(log.PropertyNameAsString(0), Is.EqualTo("fact"));
                    Assert.That(log.PropertyValueAsString(0), Is.EqualTo("A template can just be a property."));
                };
            Log.Trace.T4("{fact}", "A template can just be a property.");

            _sink.OnIngest = log => Assert.That(log.Properties.Count, Is.EqualTo(0));
            Log.Trace.T5("A template is allowed to have zero properties.");

            _sink.OnIngest = log =>
                {
                    Assert.That(log.Properties.Count, Is.EqualTo(3));
                    Assert.That(log.PropertyNameAsString(0), Is.EqualTo("part1"));
                    Assert.That(log.PropertyNameAsString(1), Is.EqualTo("part2"));
                    Assert.That(log.PropertyNameAsString(2), Is.EqualTo("part3"));

                    Assert.That(log.PropertyValueAsString(0), Is.EqualTo("It is allowed"));
                    Assert.That(log.PropertyValueAsString(1), Is.EqualTo(" to have multiple properties "));
                    Assert.That(log.PropertyValueAsString(2), Is.EqualTo(" back to back."));
                };
            Log.Trace.T6("{part1}{part2}{part3}", "It is allowed", " to have multiple properties ", " back to back.");
        }

        [Test]
        public void CanFormatPositionalProperties()
        {
            // formatting is culture specific
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            _sink.OnIngest = log =>
                {
                    Assert.That(log.Properties.Count, Is.EqualTo(3));
                    Assert.That(log.PropertyNameAsString(0), Is.EqualTo("number"));
                    Assert.That(log.PropertyNameAsString(1), Is.EqualTo("date"));
                    Assert.That(log.PropertyNameAsString(2), Is.EqualTo("custom"));

                    Assert.That(log.PropertyValueAsString(0), Is.EqualTo("1.23450"));
                    Assert.That(log.PropertyValueAsString(1), Is.EqualTo("08/07/1999"));
                };
            var custom = new ClassWithCustomToStringMethod();
            Log.Trace.T7("{number:F5} {date:d}{custom:some_string passed|<as?> format}", 1.2345, new DateTime(1999, 8, 7), custom);
            Assert.That(custom.LastReceivedToStringFormat, Is.EqualTo("some_string passed|<as?> format"));

            // Try a different culture
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            _sink.OnIngest = log =>
                {
                    Assert.That(log.Properties.Count, Is.EqualTo(1));
                    Assert.That(log.PropertyNameAsString(0), Is.EqualTo("date"));
                    Assert.That(log.PropertyValueAsString(0), Is.EqualTo("7.08.1999"));
                };
            Log.Trace.T8("{date:d}", new DateTime(1999, 8, 7));

        }

        // TODO We need this class public/internal today to make it accessible from within the Log.Error...() method.
        // We could instead make the log method generic 'where T : object' which will allow us to call ToString without
        // boxing any values. Same for 'where T : IUtf8SpanFormattable'.
        public class ClassWithCustomToStringMethod
        {
            public string? LastReceivedToStringFormat { get; private set; }

            public string? ToString(string? format)
            {
                LastReceivedToStringFormat = format;
                return "some_value";
            }
        }
    }
}

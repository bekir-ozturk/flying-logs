using System.Globalization;
using System.Text;

using FlyingLogs.Core;
using FlyingLogs.Shared;

namespace FlyingLogs.UseCaseTests
{
    internal class BasicPropertyTypeTests
    {
        private readonly TestSink _sink = new ();
        
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Configuration.Initialize(_sink);
        }

        [Test]
        public void CanCorrectlyIdentifyBasicTypes()
        {
            int ingestionCount = 0;
            _sink.OnIngest = (template, values) => 
            {
                Assert.That(template.PropertyTypes.Span[0], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[1], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[2], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[3], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[4], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[5], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[6], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[7], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[8], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[9], Is.EqualTo(BasicPropertyType.Integer));
                Assert.That(template.PropertyTypes.Span[10], Is.EqualTo(BasicPropertyType.Fraction));
                Assert.That(template.PropertyTypes.Span[11], Is.EqualTo(BasicPropertyType.Fraction));
                Assert.That(template.PropertyTypes.Span[12], Is.EqualTo(BasicPropertyType.Fraction));
                Assert.That(template.PropertyTypes.Span[13], Is.EqualTo(BasicPropertyType.Bool));
                Assert.That(template.PropertyTypes.Span[14], Is.EqualTo(BasicPropertyType.String));
                Assert.That(template.PropertyTypes.Span[15], Is.EqualTo(BasicPropertyType.String));
                Assert.That(template.PropertyTypes.Span[16], Is.EqualTo(BasicPropertyType.DateTime));
                Assert.That(template.PropertyTypes.Span[17], Is.EqualTo(BasicPropertyType.String));
                ingestionCount++;
            };
            Log.Trace.B1("Here is a log with {propertyCount} properties.", 
                i:(int)0, ui:(System.UInt32)0, b:(byte)0, sb:(sbyte)0,
                l:(long)0, ul:(ulong)0, s:(short)0, us:(ushort)0,
                ni:(nint)0, nu:(nuint)0, f:(float)0, d:(double)0,
                m:(decimal)0, bo:true, c:(char)'\0', str:string.Empty,
                dt:DateTime.UtcNow, p:(System.Drawing.Point)default
            );
            Assert.That(ingestionCount, Is.EqualTo(1));
        }
    }
}

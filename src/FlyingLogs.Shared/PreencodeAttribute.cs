using System;

namespace FlyingLogs.Core
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PreencodeAttribute : Attribute
    {
        public PreencodeAttribute(LogEncodings encoding) { }
    }
}
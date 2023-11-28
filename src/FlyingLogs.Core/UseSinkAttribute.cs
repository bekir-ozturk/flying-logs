using FlyingLogs.Shared;

namespace FlyingLogs
{
    /// <summary>
    /// Tells FlyingLogs to use the given sink when emitting logs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class UseSinkAttribute : Attribute
    {
        /// <summary> Tells FlyingLogs to use the given sink when emitting logs. This attribute only impacts the
        /// assembly it is declared on. If you want to use the same sink in depending assemblies as well, you need to
        /// decorate them all with this attribute. </summary>
        /// <param name="sinkType">Type of the sink.</param>
        /// <param name="minimumLogLevel">Minimum level of log that will be sent to this sink. If the level of the log
        /// being emitted is lower than this value, it will skip the sink. This check is done at compile time and has
        /// no runtime cost, but it cannot be changed at runtime.</param>
        /// <param name="dynamicLogLevel"> Determines whether you can change the minimum log level at runtime. If set
        /// to true, the level can be changed at runtime using the appropriate overload of
        /// <code>FlyingLogs.SetMinimumLevelForSink<T>(LogLevel)</code>. If set to false, only
        /// <see cref="MinimumLogLevel"/> is used to determine whether a log should be emitted or skipped. </param>
        public UseSinkAttribute(Type sinkType, LogLevel minimumLogLevel = LogLevel.Information, bool dynamicLogLevel = false) { }
    }
}

namespace FlyingLogs.Sinks
{
    public class SeqHttpSinkConfig
    {
        public string? HostAddress { get; set; } = "localhost";
        public int? PortNumber { get; set; } = 5341;
    }
}

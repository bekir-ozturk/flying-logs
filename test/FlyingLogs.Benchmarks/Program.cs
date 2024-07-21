using BenchmarkDotNet.Running;

namespace FlyingLogs.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<FlyingLogsVsSerilog>();
        }
    }
}
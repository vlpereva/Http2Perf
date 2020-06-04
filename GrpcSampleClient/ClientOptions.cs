namespace GrpcSampleClient
{
    public class ClientOptions
    {
        /// <summary>
        /// Comma separated list of protocols to test. * for all
        /// </summary>
        public string Protocols { get; set; } = "*";
        /// <summary>
        /// Frequency of statistics reporting. Default 3 seconds. Longer interval like 30 seconds will give better approximation
        /// </summary>
        public int ReportFrequency { get; set; } = 3;
        /// <summary>
        /// Test duration is seconds after which client will exit
        /// </summary>
        public int TestDuration { get; set; } = 120;
        public string RawGrpcUri { get; set; } = $"https://localhost:5001/greet.Greeter/SayHello";
        public bool ClientPerThread { get; set; } = false;
        public int Parallelism { get; set; } = 100;
    }
}
namespace GrpcSampleClient
{
    public class ClientOptions
    {
        public string Protocol { get; set; }
        public int ReportFrequency { get; set; } = 3000;
        public string RawGrpcUri { get; set; } = $"https://localhost:5001/greet.Greeter/SayHello";
        public bool ClientPerThread { get; set; } = false;
        public int Parallelism { get; set; } = 100;
    }
}
using System.Threading.Tasks;
using GrpcSample;
using Microsoft.AspNetCore.SignalR;

namespace GrpcSampleServer.Services
{
    public class GreeterHub: Hub
    {
        public async Task<HelloReply> SayHello(HelloRequest request)
        {
            return new HelloReply
            {
                Message = "Hello " + request.Name
            };
        }
    }
}
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcSample;
using Microsoft.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace GrpcSampleClient
{
    public class Program
    {
        private static readonly RecyclableMemoryStreamManager StreamPool = new RecyclableMemoryStreamManager();
        private static readonly Dictionary<int, Greeter.GreeterClient> GrpcClientCache = new Dictionary<int, Greeter.GreeterClient>();
        private static readonly Dictionary<int, HttpClient> HttpClientCache = new Dictionary<int, HttpClient>();
        private static readonly Dictionary<int, HttpMessageInvoker> HttpMessageInvokerCache = new Dictionary<int, HttpMessageInvoker>();
        
        private static readonly ClientOptions _options = new ClientOptions();
        
        static async Task Main(string[] args)
        {
            var cfg = new ConfigurationBuilder().AddCommandLine(args).Build();
            cfg.Bind(_options);
            
            Console.WriteLine("ClientPerThread: " + _options.ClientPerThread);
            Console.WriteLine("Parallelism: " + _options.Parallelism);
            Console.WriteLine($"{nameof(GCSettings.IsServerGC)}: {GCSettings.IsServerGC}");

            Func<int, Task> request;
            var allProtocols = new[] {"g", "c", "r", "h1", "h2"};
            var selectedProtocols = _options.Protocols.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (selectedProtocols.Contains("*"))
                selectedProtocols = allProtocols;
            foreach (var protocol in selectedProtocols)
            {
                long successCounter = 0;
                long errorCounter = 0;
                long aggregateLatency = 0;
                Exception lastError = null;
                var timeIsUp = false;

                new Thread(() =>
                    {
                        var testRuntime = Stopwatch.StartNew();
                        var sw = Stopwatch.StartNew();
                        while (!timeIsUp)
                        {
                            sw.Restart();
                            successCounter = 0;
                            aggregateLatency = 0;
                            errorCounter = 0;
                            Thread.Sleep(_options.ReportFrequency * 1000);
                            var ex = lastError;
                            Console.WriteLine($"RPS {successCounter/sw.Elapsed.TotalSeconds:F0}; Errors {errorCounter}; Last elapsed {sw.ElapsedMilliseconds}ms; Latency {aggregateLatency/(successCounter + 1)*1000d/Stopwatch.Frequency:F3}ms");
                            if (ex != null)
                            {
                                Console.WriteLine(ex.ToString());
                            }

                            if (testRuntime.Elapsed.TotalSeconds > _options.TestDuration)
                                timeIsUp = true;
                        }
                    })
                    .Start();

                string clientType;
                switch (protocol)
                {
                    case "g":
                        request = (i) => MakeGrpcCall(new HelloRequest() {Name = "foo"}, GetGrpcNetClient(i));
                        clientType = "Grpc.Net.Client";
                        break;
                    case "c":
                        request = (i) => MakeGrpcCall(new HelloRequest() {Name = "foo"}, GetGrpcCoreClient(i));
                        clientType = "Grpc.Core";
                        break;
                    case "r":
                        request = (i) => MakeRawGrpcCall(new HelloRequest() {Name = "foo"}, GetHttpMessageInvoker(i),
                            streamRequest: false, streamResponse: false);
                        clientType = "Raw HttpMessageInvoker";
                        break;
                    case "r-stream-request":
                        request = (i) => MakeRawGrpcCall(new HelloRequest() {Name = "foo"}, GetHttpMessageInvoker(i),
                            streamRequest: true, streamResponse: false);
                        clientType = "Raw HttpMessageInvoker";
                        break;
                    case "r-stream-response":
                        request = (i) => MakeRawGrpcCall(new HelloRequest() {Name = "foo"}, GetHttpMessageInvoker(i),
                            streamRequest: false, streamResponse: true);
                        clientType = "Raw HttpMessageInvoker";
                        break;
                    case "r-stream-all":
                        request = (i) => MakeRawGrpcCall(new HelloRequest() {Name = "foo"}, GetHttpMessageInvoker(i),
                            streamRequest: true, streamResponse: true);
                        clientType = "Raw HttpMessageInvoker";
                        break;
                    case "h2":
                        request = (i) => MakeHttpCall(new HelloRequest() {Name = "foo"}, GetHttpClient(i, 5001),
                            HttpVersion.Version20);
                        clientType = "HttpClient+HTTP/2";
                        break;
                    case "h1":
                        request = (i) => MakeHttpCall(new HelloRequest() {Name = "foo"}, GetHttpClient(i, 5000),
                            HttpVersion.Version11);
                        clientType = "HttpClient+HTTP/1.1";
                        break;
                    default:
                        Console.WriteLine("Specify --Protocol option");
                        return;
                }

                Console.WriteLine(clientType);
                
                await Task.WhenAll(Enumerable.Range(0,  _options.Parallelism).Select(async i =>
                {
                    var sw = Stopwatch.StartNew();
                    while (!timeIsUp)
                    {
                        sw.Restart();
                        try
                        {
                            await request(i);
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            Interlocked.Increment(ref errorCounter);
                        }

                        Interlocked.Add(ref aggregateLatency, sw.ElapsedTicks);

                        Interlocked.Increment(ref successCounter);
                    }
                }));
            }
        }

        private static Greeter.GreeterClient GetGrpcNetClient(int i)
        {
            if (!_options.ClientPerThread)
            {
                i = 0;
            }
            if (!GrpcClientCache.TryGetValue(i, out var client))
            {
                client = GetGrpcNetClient("localhost", 5001);
                GrpcClientCache.Add(i, client);
            }

            return client;
        }

        private static Greeter.GreeterClient GetGrpcCoreClient(int i)
        {
            if (!_options.ClientPerThread)
            {
                i = 0;
            }
            if (!GrpcClientCache.TryGetValue(i, out var client))
            {
                client = GetGrpcCoreClient("localhost", 5001);
                GrpcClientCache.Add(i, client);
            }

            return client;
        }

        private static HttpClient GetHttpClient(int i, int port)
        {
            if (!_options.ClientPerThread)
            {
                i = 0;
            }
            if (!HttpClientCache.TryGetValue(i, out var client))
            {
                client = new HttpClient { BaseAddress = new Uri("https://localhost:" + port) };
                HttpClientCache.Add(i, client);
            }

            return client;
        }

        private static HttpMessageInvoker GetHttpMessageInvoker(int i)
        {
            if (!_options.ClientPerThread)
            {
                i = 0;
            }
            if (!HttpMessageInvokerCache.TryGetValue(i, out var client))
            {
                client = new HttpMessageInvoker(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
                HttpMessageInvokerCache.Add(i, client);
            }

            return client;
        }

        private static Greeter.GreeterClient GetGrpcNetClient(string host, int port)
        {
            var httpHandler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
            var baseUri = new UriBuilder
            {
                Scheme = Uri.UriSchemeHttps,
                Host = host,
                Port = port

            };
            var channelOptions = new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            };
            return new Greeter.GreeterClient(GrpcChannel.ForAddress(baseUri.Uri, channelOptions));
        }

        private static Greeter.GreeterClient GetGrpcCoreClient(string host, int port)
        {
            var channel = new Channel(host + ":" + port, ChannelCredentials.Insecure);
            return new Greeter.GreeterClient(channel);
        }

        private static async Task<HelloReply> MakeGrpcCall(HelloRequest request, Greeter.GreeterClient client)
        {
            return await client.SayHelloAsync(request);
        }

        private static async Task<HelloReply> MakeHttpCall(HelloRequest request, HttpClient client, Version httpVersion)
        {
            using var memStream = StreamPool.GetStream();
            request.WriteDelimitedTo(memStream);
            memStream.Position = 0;
            using var httpRequest = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                Content = new StreamContent(memStream),
                Version = httpVersion
            };
            httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
            using var httpResponse = await client.SendAsync(httpRequest);
            var responseStream = await httpResponse.Content.ReadAsStreamAsync();
            return HelloReply.Parser.ParseDelimitedFrom(responseStream);
        }

        private const int HeaderSize = 5;
        private static readonly CancellationTokenSource Cts = new CancellationTokenSource();

        private static async Task<HelloReply> MakeRawGrpcCall(HelloRequest request, HttpMessageInvoker client, bool streamRequest, bool streamResponse)
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.RawGrpcUri);
            httpRequest.Version = HttpVersion.Version20;

            if (!streamRequest)
            {
                var messageSize = request.CalculateSize();
                var data = new byte[messageSize + HeaderSize];
                request.WriteTo(new CodedOutputStream(data));

                Array.Copy(data, 0, data, HeaderSize, messageSize);
                data[0] = 0;
                BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(1, 4), (uint)messageSize);

                httpRequest.Content = new ByteArrayContent(data);
                httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", "application/grpc");
            }
            else
            {
                httpRequest.Content = new PushUnaryContent<HelloRequest>(request);
            }

            httpRequest.Headers.TryAddWithoutValidation("TE", "trailers");

            using var response = await client.SendAsync(httpRequest, Cts.Token);
            response.EnsureSuccessStatusCode();

            HelloReply responseMessage;
            if (!streamResponse)
            {
                var data = await response.Content.ReadAsByteArrayAsync();
                responseMessage = HelloReply.Parser.ParseFrom(data.AsSpan(5).ToArray());
            }
            else
            {
                var responseStream = await response.Content.ReadAsStreamAsync();
                var data = new byte[HeaderSize];

                int read;
                var received = 0;
                while ((read = await responseStream.ReadAsync(data.AsMemory(received, HeaderSize - received), Cts.Token).ConfigureAwait(false)) > 0)
                {
                    received += read;

                    if (received == HeaderSize)
                    {
                        break;
                    }
                }

                if (received < HeaderSize)
                {
                    throw new InvalidDataException("Unexpected end of content while reading the message header.");
                }

                var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(1, 4));

                if (data.Length < length)
                {
                    data = new byte[length];
                }

                received = 0;
                while ((read = await responseStream.ReadAsync(data.AsMemory(received, length - received), Cts.Token).ConfigureAwait(false)) > 0)
                {
                    received += read;

                    if (received == length)
                    {
                        break;
                    }
                }

                read = await responseStream.ReadAsync(data, Cts.Token);
                if (read > 0)
                {
                    throw new InvalidDataException("Extra data returned.");
                }

                responseMessage = HelloReply.Parser.ParseFrom(data);
            }

            var grpcStatus = response.TrailingHeaders.GetValues("grpc-status").SingleOrDefault();
            if (grpcStatus != "0")
            {
                throw new InvalidOperationException($"Unexpected grpc-status: {grpcStatus}");
            }

            return responseMessage;
        }
    }
}

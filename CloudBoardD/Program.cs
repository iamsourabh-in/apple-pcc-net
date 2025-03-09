// CloudBoardD/Program.cs
using CloudBoardCommonAPI;
using CloudBoardCommon;
using CloudBoardHealth;
using CloudBoardMetrics;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using Prometheus.Client.AspNetCore;
using System.Buffers;
using Google.Protobuf;
using Prometheus;
using Serilog;
using Polly;
using Consul;
using NodeInfo = CloudBoardCommon.NodeInfo;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;

// Define a simple gRPC service for health checks
public class HealthCheckService //: CloudBoardCommon.HealthCheck.HealthCheckBase
{
    private readonly IHealthCheck _healthCheck;

    public HealthCheckService(IHealthCheck healthCheck)
    {
        _healthCheck = healthCheck;
    }

    public async Task<HealthCheckResponse> Check(HealthCheckRequest request, ServerCallContext context)
    {
        bool isHealthy = await _healthCheck.IsHealthyAsync(context.CancellationToken);
        return new HealthCheckResponse { Status = isHealthy ? "SERVING" : "NOT_SERVING" };
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Any, 9001, o => o.Protocols = HttpProtocols.Http1AndHttp2); // Or specify a different port
        });
        // ... inside the Program class ...
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration) //Use configuration for logging setup
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", "CloudBoardD")
                .WriteTo.Console();
        });

        NodeInfo nodeInfo = new NodeInfo();
        if (nodeInfo.IsLeader)
        {
            Console.WriteLine("This is the leader node.");
        }
        else
        {
            Console.WriteLine("This is not the leader node.");
            return;
        }

        //Use Polly for retries in HealthCheck
        var healthCheckPolicy = Polly.Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt));

        // Configure app settings from JSON
        builder.Configuration.AddJsonFile("appsettings.json", optional: false,  reloadOnChange: true);

        // Add services to the container.
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<IHealthCheck, CloudBoardHealthCheck>();
        builder.Services.AddSingleton<HealthCheckService>(); // Add the gRPC service
        builder.Services.AddSingleton<IJobServiceClient>(provider => new GrpcJobServiceClient(builder.Configuration["JobHelperAddress"]));
        var app = builder.Build();
        var consulClient = new ConsulClient(c =>
        {
            c.Address = new Uri("http://localhost:8500"); //Consul address
        });

        //Register the service
        var registration = new AgentServiceRegistration
        {
            ID = Guid.NewGuid().ToString(),
            Name = "CloudBoardD",
            Address = "localhost",
            Port = 5002, //Your gRPC port
            Tags = new[] { "cloudboard" }
        };

        await consulClient.Agent.ServiceRegister(registration);

        // Configure the HTTP request pipeline.
        app.MapGet("/health", async (HttpContext context) =>
        {
            bool isHealthy = await app.Services.GetRequiredService<IHealthCheck>().IsHealthyAsync(context.RequestAborted);
            await context.Response.WriteAsync(isHealthy ? "OK" : "FAILED");
        });

        app.MapGrpcService<HealthCheckService>(); // Map the gRPC service



        // ... later, inside the app.MapGrpcService ...
        app.MapGet("/job", async (HttpContext context, IJobServiceClient jobClient) =>
        {
            // Create a buffer to accumulate the data
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();

            // Read from the PipeReader
            while (true)
            {
                var result = await context.Request.BodyReader.ReadAsync();
                var readBuffer = result.Buffer;

                // Copy the data to the buffer
                foreach (var segment in readBuffer)
                {
                    buffer.Write(segment.Span);
                }

                // Mark the data as consumed
                context.Request.BodyReader.AdvanceTo(readBuffer.End);

                // Break if the end of the stream is reached
                if (result.IsCompleted)
                {
                    break;
                }
            }

            // Convert the buffer to a string (assuming UTF-8 encoding)
            var requestData = System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);

            var jobRequest = new CloudBoardJobAPI.JobRequest
            {
                JobId = Guid.NewGuid().ToString(),
                RequestData = ByteString.CopyFromUtf8(requestData)
            };

            // Send the job request to the job helper
            var response = await jobClient.HandleJobRequestAsync(jobRequest, context.RequestAborted);

            // Send the response back to the client
            await context.Response.WriteAsJsonAsync(response);
        });

        // Start Prometheus metrics server
        var metricServer = new MetricServer(builder.Configuration["PrometheusMetricsPort"]);
        await metricServer.StartAsync();

        CloudBoardMetrics.Metrics.CloudBoardUptime.IncTo(0); // Initialize Uptime

        //Periodically update metrics
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                CloudBoardMetrics.Metrics.CloudBoardUptime.Inc();
                await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
            }
        }, cts.Token);

        // Run the application
        await app.RunAsync();
        cts.Cancel(); // Signal to stop updating metrics

        await metricServer.StopAsync();
    }
}


//Helper for Prometheus
public class MetricServer
{
    private readonly int _port;
    private readonly IWebHost _webHost;

    public MetricServer(string port)
    {
        _port = int.TryParse(port, out int p) ? p : 9100; //Default port
        _webHost = new WebHostBuilder()
            .UseKestrel()
            .Configure(app => app.UseMetricServer())
            .Build();
    }

    public Task StartAsync() => _webHost.StartAsync();
    public Task StopAsync() => _webHost.StopAsync();
}

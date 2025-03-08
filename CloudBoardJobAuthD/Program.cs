// CloudBoardJobAuthD/Program.cs
using CloudBoardJobAuthAPI;
using Grpc.Core;
using Prometheus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CloudBoardCommon;

public class JobAuthService : CloudBoardJobAuthAPI.JobAuth.JobAuthBase
{
    private readonly List<byte[]> _tgtKeys = new(); // Replace with your key management
    private readonly List<byte[]> _ottKeys = new(); // Replace with your key management

    public override async Task RequestTGTSigningKeys(Request request, IServerStreamWriter<KeyResponse> responseStream, ServerCallContext context)
    {
      await foreach (var key in _tgtKeys.ToAsyncEnumerable(context.CancellationToken))
      {
          await responseStream.WriteAsync(new KeyResponse { PublicKeyDer = Google.Protobuf.ByteString.CopyFrom(key) });
      }
    }

    public override async Task RequestOTTSigningKeys(Request request, IServerStreamWriter<KeyResponse> responseStream, ServerCallContext context)
    {
      await foreach (var key in _ottKeys.ToAsyncEnumerable(context.CancellationToken))
      {
          await responseStream.WriteAsync(new KeyResponse { PublicKeyDer = Google.Protobuf.ByteString.CopyFrom(key) });
      }
    }
}


public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        // Add services to the container.
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<JobAuthService>();
        var app = builder.Build();

        app.MapGrpcService<JobAuthService>();


        // Start Prometheus metrics server
        var metricServer = new MetricServer(builder.Configuration["PrometheusMetricsPort"]);
        await metricServer.StartAsync();

        //Periodically update metrics
        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                //Add your metrics here
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
            .UseUrls($"http://*:{_port}/metrics")
            .Configure(app => app.UseMetricServer())
            .Build();
    }

    public Task StartAsync() => _webHost.StartAsync();
    public Task StopAsync() => _webHost.StopAsync();
}


public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}

// CloudBoardJobHelper/Program.cs
using CloudBoardCommon;
using CloudBoardJobAuthAPI;
using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Prometheus;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Any, 9002, o => o.Protocols = HttpProtocols.Http1AndHttp2); // Or specify a different port
        });
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        // Add services to the container.
        builder.Services.AddGrpc();
        var app = builder.Build();

        // Map the gRPC service
        app.MapGrpcService<JobHelperService>();

        // Start Prometheus metrics server
        var metricServer = new MetricServer(builder.Configuration["PrometheusMetricsPort"]);
        await metricServer.StartAsync();


        // Run the application
        await app.RunAsync();
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
        _port = int.TryParse(port, out int p) ? p : 9102; //Default port
        _webHost = new WebHostBuilder()
            .UseKestrel()
            .Configure(app => app.UseMetricServer())
            .Build();
    }

    public Task StartAsync() => _webHost.StartAsync();
    public Task StopAsync() => _webHost.StopAsync();
}


public class JobHelperService : CloudBoardCommon.IJobHelperAPI
{
    private readonly IJobAuthClient _jobAuthClient; // Replace with your actual authentication client
    private readonly string _workloadPath;         // Path to your workload executable
    public JobHelperService(IJobAuthClient jobAuthClient, string workloadPath)
    {
        _jobAuthClient = jobAuthClient;
        _workloadPath = workloadPath;
    }

    public async Task<JobResponse> HandleJobRequestAsync(JobRequest request, CancellationToken cancellationToken)
    {
        // 1. Authentication:  (Simplified - replace with actual authentication)
        try
        {
            //Call to the JobAuthClient
            var keys = await _jobAuthClient.RequestTGTSigningKeysAsync(cancellationToken);
            if(keys == null || keys.Count() == 0)
            {
                throw new Exception("Error receiving keys");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            throw;
        }


        // 2. Job Execution: (Simplified - replace with your workload execution)
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _workloadPath,
                Arguments = request.RequestData, // Pass request data as arguments
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        // 3. Response:
        return new JobResponse
        {
            JobId = request.JobId,
            ResponseData = output + error // Combine output and error
        };
    }
}

public class GrpcJobAuthClient : IJobAuthClient
{
    private readonly JobAuth.JobAuthClient _client;
    private readonly AsyncServerStreamingCall<KeyResponse> _tgtCall;
    private readonly AsyncServerStreamingCall<KeyResponse> _ottCall;

    public GrpcJobAuthClient(string jobAuthAddress)
    {
        var channel = GrpcChannel.ForAddress(jobAuthAddress);
        _client = new JobAuth.JobAuthClient(channel);

        // Correctly initialize the server streaming calls
        _tgtCall = _client.RequestTGTSigningKeys(new Request());
        _ottCall = _client.RequestOTTSigningKeys(new Request());
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // No connection needed, it is established on creation
    }

    public async Task<IEnumerable<byte[]>> RequestTGTSigningKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new List<byte[]>();
        try
        {
            await foreach (var key in _tgtCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                keys.Add(key.PublicKeyDer.ToArray());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error requesting TGT keys: {ex}");
        }
        return keys;
    }

    public async Task<IEnumerable<byte[]>> RequestOTTSigningKeysAsync(CancellationToken cancellationToken)
    {
        var keys = new List<byte[]>();
        try
        {
            await foreach (var key in _ottCall.ResponseStream.ReadAllAsync(cancellationToken))
            {
                keys.Add(key.PublicKeyDer.ToArray());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error requesting OTT keys: {ex}");
        }
        return keys;
    }
}


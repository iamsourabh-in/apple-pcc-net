// CloudBoardAttestationD/Program.cs
// ... other usings ...
using CloudBoardAttestationAPI;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Prometheus;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

public class AttestationService : CloudBoardAttestationAPI.AttestationService.AttestationServiceBase
{
    private readonly RSAParameters _keyPair; // Simulate SEP-backed key

    public AttestationService(IConfiguration configuration)
    {
        // Simulate key generation (INSECURE - for testing only)
        using (var rsa = RSA.Create(2048))
        {
            _keyPair = rsa.ExportParameters(true);
        }

        // Load key from secure storage in production
    }

    public override async Task<AttestationResponse> GetAttestation(AttestationRequest request, ServerCallContext context)
    {
        // Simulate attestation generation (INSECURE - for testing only)
        var attestationBundle = GenerateAttestationBundle();
        return new AttestationResponse { AttestationBundle = ByteString.CopyFrom(attestationBundle) };
    }

    private byte[] GenerateAttestationBundle()
    {
        // Simulate creating an attestation bundle.  Replace with your actual attestation logic.
        // This is a placeholder and should be replaced by a real attestation system.
        using (var rsa = RSA.Create())
        {
            rsa.ImportParameters(_keyPair);
            var dataToSign = Guid.NewGuid().ToByteArray();
            return rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

    }

    public override async Task<AttestedKeySet> GetAttestedKeySet(AttestationRequest request, ServerCallContext context)
    {
        return await CreateAttestedKeySetAsync(context.CancellationToken);
    }


    public override Task<AttestationResponse> AttestationRotated(AttestationSet request, ServerCallContext context)
    {
        //Handle rotated attestation
        return Task.FromResult(new AttestationResponse());
    }

    private async Task<AttestationResponse> CreateAttestationAsync(CancellationToken cancellationToken)
    {
        // Simulate attestation generation (INSECURE - replace with secure attestation for production)
        return new AttestationResponse { AttestationBundle = ByteString.CopyFrom(GenerateAttestationBundle()) };
    }


    private async Task<AttestedKeySet> CreateAttestedKeySetAsync(CancellationToken cancellationToken)
    {
        return new AttestedKeySet
        {
            CurrentKey = await CreateAttestedKeyAsync(cancellationToken),
            //Add unpublished keys here
        };

    }

    public async Task<AttestedKey> CreateAttestedKeyAsync(CancellationToken cancellationToken)
    {
        // Simulate creating an attested key (INSECURE - replace with secure key generation for production)
        return new AttestedKey
        {
            KeyData = ByteString.CopyFrom(GenerateAttestationBundle()),
            KeyId = Guid.NewGuid().ToString(),
            Expiry = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds(),
            PublicationExpiry = DateTimeOffset.UtcNow.AddDays(0.5).ToUnixTimeMilliseconds(), // Half life
        };

    }
}


public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        builder.Services.AddGrpc();
        builder.Services.AddSingleton<AttestationService>();
        var app = builder.Build();

        app.MapGrpcService<AttestationService>();

        // Start Prometheus metrics server
        var metricServer = new MetricServer(builder.Configuration["PrometheusMetricsPort"]);
        await metricServer.StartAsync();

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
        _port = int.TryParse(port, out int p) ? p : 9103; //Default port
        _webHost = new WebHostBuilder()
            .UseKestrel()
            .Configure(app => app.UseMetricServer())
            .Build();
    }

    public Task StartAsync() => _webHost.StartAsync();
    public Task StopAsync() => _webHost.StopAsync();
}


using CloudBoardAttestationAPI; // Assuming this exists
using CloudBoardCommon;
using CloudBoardD.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text.Json; // For structured logging

// ... other necessary using statements ...
public class CloudBoardDaemon : IAsyncDisposable
{
    private readonly ILogger<CloudBoardDaemon> _logger;
    private readonly CloudBoardConfiguration _config;
    private readonly IMetricsSystem _metricsSystem;
    private readonly ITracer _tracer;
    private readonly ServiceHealthMonitor _healthMonitor;
    private readonly HeartbeatPublisher? _heartbeatPublisher;
    private readonly IRequestFielderManager? _requestFielderManager;
    private readonly IJobHelperClientProvider _jobHelperClientProvider;
    private readonly IServiceDiscoveryPublisher? _serviceDiscovery;
    private readonly IHealthServer? _healthServer;
    private readonly IAttestationProvider? _attestationProvider;
    private readonly IHotPropertiesController? _hotProperties;
    private readonly NodeInfo? _nodeInfo;
    private readonly IWorkloadController? _workloadController;
    private readonly bool _insecureListener;
    private readonly ILifecycleManager _lifecycleManager;
    private readonly IStatusMonitor _statusMonitor;
    private readonly IJobAuthClient? _jobAuthClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _runningTasks = new();

    public CloudBoardDaemon(string? configPath = null, IMetricsSystem? metricsSystem = null)
    {
        // Create logger
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        _logger = loggerFactory.CreateLogger<CloudBoardDaemon>();

        // Load node info
        _nodeInfo = NodeInfo.Load();
        if (_nodeInfo.IsLeader.HasValue && !_nodeInfo.IsLeader.Value)
        {
            _logger.LogInformation("Not a leader node, exiting..");
            Environment.Exit(0);
        }
        else if (!_nodeInfo.IsLeader.HasValue)
        {
            _logger.LogError("Unable to check if node is a leader");
        }

        // Load configuration
        if (configPath != null)
        {
            _logger.LogInformation("Loading configuration from {ConfigPath}", configPath);
            _config = CloudBoardConfiguration.FromFile(configPath, new RealSecureConfigLoader());
        }
        else
        {
            _logger.LogInformation("Loading configuration from environment");
            _config = CloudBoardConfiguration.FromEnvironment();
        }

        var configJson = JsonSerializer.Serialize(_config);
        _logger.LogInformation("Loaded configuration: {Config}", configJson);

        // Initialize metrics
        _metricsSystem = metricsSystem ?? new CloudMetricsSystem("cloudboardd");
        _statusMonitor = new StatusMonitor(_metricsSystem);
        _tracer = new RequestSummaryTracer(_metricsSystem);

        // Initialize health monitoring
        _healthMonitor = new ServiceHealthMonitor();
        _healthServer = new HealthServer();

        // Initialize request handling
        _requestFielderManager = new RequestFielderManager(
            _config.Prewarming?.PrewarmedPoolSize ?? 3,
            _config.Prewarming?.MaxProcessCount ?? 3,
            _metricsSystem,
            _tracer);

        _jobHelperClientProvider = new JobHelperClientProvider(_requestFielderManager);

        // Initialize hot properties
        _hotProperties = new HotPropertiesController();

        // Initialize workload controller
        _workloadController = new WorkloadController(_healthMonitor, _metricsSystem);

        // Initialize heartbeat publisher if configured
        if (_config.Heartbeat != null)
        {
            _logger.LogInformation("Heartbeat configured");
            _heartbeatPublisher = new HeartbeatPublisher(
                _config.Heartbeat,
                Environment.MachineName,
                _nodeInfo,
                _statusMonitor,
                _hotProperties,
                _metricsSystem);
        }
        else
        {
            _logger.LogInformation("Heartbeat not configured");
        }

        // Initialize lifecycle manager
        var lifecycleManagerConfig = _config.LifecycleManager ?? new LifecycleManagerConfig();
        _lifecycleManager = new LifecycleManager(new LifecycleManagerOptions
        {
            Timeout = lifecycleManagerConfig.DrainTimeout
        });

        _insecureListener = false;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting CloudBoard daemon");
        _statusMonitor.Initializing();

        try
        {
            // Clean up any managed jobs
            await LaunchdJobHelper.CleanupManagedLaunchdJobsAsync(_logger);

            // Start job quiescence monitor
            var jobQuiescenceMonitor = new JobQuiescenceMonitor(_lifecycleManager);
            await jobQuiescenceMonitor.StartQuiescenceMonitorAsync();

            await _lifecycleManager.ManagedAsync(async () =>
            {
                // Check identity for secure service
                var identityManager = new IdentityManager();
                if (!_insecureListener && identityManager.Identity == null)
                {
                    _logger.LogError("Unable to load TLS identity, exiting.");
                    throw new IdentityManagerException("Unable to run secure service");
                }

                // Log hot properties status
                if (_hotProperties != null)
                {
                    _logger.LogInformation("Hot properties are enabled.");
                }
                else
                {
                    _logger.LogInformation("Hot properties are disabled.");
                }

                // Log heartbeat status
                if (_heartbeatPublisher != null)
                {
                    _logger.LogInformation("Heartbeats are enabled.");
                    await _heartbeatPublisher.UpdateCredentialProviderAsync(() =>
                        identityManager.Identity?.Credential);
                }
                else
                {
                    _logger.LogInformation("Heartbeats are disabled.");
                }

                // Resolve service address
                var serviceAddress = await ResolveServiceAddressAsync();

                // Initialize service discovery if configured
                IServiceDiscoveryPublisher? serviceDiscovery = null;
                if (_config.ServiceDiscovery != null)
                {
                    _logger.LogInformation("Enabling service discovery");
                    serviceDiscovery = new ServiceDiscoveryPublisher(
                        _config.ServiceDiscovery,
                        serviceAddress,
                        identityManager.IdentityCallback,
                        _hotProperties,
                        _nodeInfo,
                        _config.ServiceDiscovery.CellID,
                        _statusMonitor,
                        _metricsSystem);
                }
                else
                {
                    _logger.LogWarning("Service discovery not enabled");
                }

                // Initialize health provider
                var healthProvider = new HealthProvider(_healthMonitor);

                // Initialize attestation provider
                var attestationProvider = new AttestationProvider(_metricsSystem);

                // Initialize CloudBoard provider
                var cloudBoardProvider = new CloudBoardProvider(
                    _jobHelperClientProvider,
                    _healthMonitor,
                    _metricsSystem,
                    _tracer,
                    attestationProvider,
                    _config.Load,
                    _hotProperties);

                // Start all services in parallel
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _cts.Token);

                // Start request fielder manager
                if (_requestFielderManager != null)
                {
                    _runningTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _requestFielderManager.RunAsync(linkedCts.Token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Request fielder manager failed");
                            _cts.Cancel();
                        }
                    }));
                }

                // Start attestation provider
                _runningTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await attestationProvider.RunAsync(linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Attestation provider failed");
                        _cts.Cancel();
                    }
                }));

                // Verify attestation fetch
                _statusMonitor.WaitingForFirstAttestationFetch();
                _ = await attestationProvider.CurrentAttestationSetAsync();

                // Verify presence of auth token signing keys if configured
                if (_config.BlockHealthinessOnAuthSigningKeysPresence)
                {
                    _statusMonitor.WaitingForFirstKeyFetch();
                    await CheckAuthTokenSigningKeysPresenceAsync();
                    _logger.LogInformation("Signing key verification passed");
                }
                else
                {
                    _logger.LogInformation("Skipping verification due to config's BlockHealthinessOnAuthSigningKeysPresence");
                }

                _statusMonitor.WaitingForFirstHotPropertyUpdate();

                // Start CloudBoard provider
                _runningTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await cloudBoardProvider.RunAsync(linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CloudBoard provider failed");
                        _cts.Cancel();
                    }
                }));

                // Start identity update loop
                _runningTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await identityManager.IdentityUpdateLoopAsync(linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Identity update loop failed");
                        _cts.Cancel();
                    }
                }));

                // Start service discovery if configured
                if (serviceDiscovery != null)
                {
                    _runningTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            if (_hotProperties != null)
                            {
                                await _hotProperties.WaitForFirstUpdateAsync();
                            }
                            _statusMonitor.WaitingForWorkloadRegistration();
                            await serviceDiscovery.RunAsync(linkedCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _statusMonitor.ServiceDiscoveryRunningFailed();
                            _logger.LogError(ex, "Service discovery failed");
                            _cts.Cancel();
                        }
                    }));
                }

                // Start health monitor
                _runningTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (_hotProperties != null)
                        {
                            await _hotProperties.WaitForFirstUpdateAsync();
                        }
                        await healthProvider.RunAsync(linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Health monitor failed");
                        _cts.Cancel();
                    }
                }));

                // Start health server if configured
                if (_healthServer != null)
                {
                    _runningTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _healthServer.RunAsync(_healthMonitor, linkedCts.Token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Health server failed");
                            _cts.Cancel();
                        }
                    }));
                }

                // Start heartbeat publisher if configured
                if (_heartbeatPublisher != null)
                {
                    _runningTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _heartbeatPublisher.RunAsync(linkedCts.Token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Heartbeat publisher failed");
                            _cts.Cancel();
                        }
                    }));
                }

                // Start gRPC server
                _runningTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await RunGrpcServerAsync(
                            cloudBoardProvider,
                            new[] { healthProvider },
                            identityManager.IdentityCallback,
                            serviceAddress,
                            _config.Grpc?.ExpectedPeerIdentifier,
                            _config.Grpc?.Keepalive,
                            _metricsSystem,
                            linkedCts.Token);
                    }
                    catch (Exception ex)
                    {
                        _statusMonitor.GrpcServerRunningFailed();
                        _logger.LogError(ex, "gRPC server failed");
                        _cts.Cancel();
                    }
                }));

                // Start workload controller if configured
                if (_workloadController != null)
                {
                    _runningTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _workloadController.RunAsync(
                                serviceDiscovery,
                                cloudBoardProvider.ConcurrentRequestCountStream,
                                cloudBoardProvider.Pause,
                                linkedCts.Token);
                        }
                        catch (Exception ex)
                        {
                            _statusMonitor.WorkloadControllerRunningFailed();
                            _logger.LogError(ex, "Workload controller failed");
                            _cts.Cancel();
                        }
                    }));
                }

                // Start hot properties controller if configured
                if (_hotProperties != null)
                {
                    _runningTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await _hotProperties.RunAsync(_metricsSystem, linkedCts.Token);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Hot properties controller failed");
                            _cts.Cancel();
                        }
                    }));
                }

                // Wait for any task to complete or fail
                await Task.WhenAny(_runningTasks);

                // Cancel all other tasks
                _cts.Cancel();

            }, onDrain: async () =>
            {
                var drainStartTime = DateTime.UtcNow;

                // Perform drain operations
                _healthMonitor.Drain();

                if (_workloadController != null)
                {
                    try
                    {
                        await _workloadController.ShutdownAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Workload controller failed to notify listeners of shutdown");
                    }
                }

                await cloudBoardProvider.DrainAsync();

                var drainDuration = DateTime.UtcNow - drainStartTime;
                _logger.LogInformation("Drain completed in: {DrainDurationSeconds}s", drainDuration.TotalSeconds);
            });
        }
        catch (Exception ex)
        {
            _statusMonitor.DaemonExitingOnError();
            _logger.LogError(ex, "Fatal error, exiting");
            throw;
        }

        _statusMonitor.DaemonDrained();
        await jobQuiescenceMonitor.QuiesceCompletedAsync();
    }

    private async Task<IPEndPoint> ResolveServiceAddressAsync()
    {
        string host = _config.Grpc?.Host ?? "localhost";
        int port = _config.Grpc?.Port ?? 0; // 0 means use any available port

        return new IPEndPoint(IPAddress.Parse(host), port);
    }

    private async Task CheckAuthTokenSigningKeysPresenceAsync()
    {
        var jobAuthClient = _jobAuthClient ?? new JobAuthClient();
        await jobAuthClient.ConnectAsync();

        var tgtSigningPublicKeyDERs = await jobAuthClient.RequestTGTSigningKeysAsync();
        var ottSigningPublicKeyDERs = await jobAuthClient.RequestOTTSigningKeysAsync();

        if (tgtSigningPublicKeyDERs.Count == 0 || ottSigningPublicKeyDERs.Count == 0)
        {
            throw new InitializationException("Missing auth token signing keys");
        }
    }

    private async Task RunGrpcServerAsync(
        CloudBoardProvider cloudBoardProvider,
        IEnumerable<ICallHandlerProvider> providers,
        Func<X509Certificate2>? identityCallback,
        IPEndPoint serviceAddress,
        string? expectedPeerIdentifier,
        KeepaliveConfig? keepalive,
        IMetricsSystem metricsSystem,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running gRPC service at {ServiceAddress}", serviceAddress);

        var builder = WebApplication.CreateBuilder();

        // Configure gRPC
        builder.Services.AddGrpc(options =>
        {
            if (keepalive != null)
            {
                options.KeepAlive = new GrpcKeepAliveOptions
                {
                    KeepAliveInterval = keepalive.Interval,
                    KeepAliveTimeout = keepalive.Timeout
                };
            }
        });

        // Configure TLS if needed
        if (identityCallback != null)
        {
            _logger.LogInformation("Running service with TLS");
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(serviceAddress.Address, serviceAddress.Port, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
                        httpsOptions.ServerCertificate = identityCallback();
                        if (!string.IsNullOrEmpty(expectedPeerIdentifier))
                        {
                            httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
                            httpsOptions.ClientCertificateValidation = (cert, chain, errors) =>
                            {
                                // Validate client certificate against expected peer identifier
                                return true; // Implement proper validation
                            };
                        }
                    });
                });
            });
        }
        else
        {
            _logger.LogWarning("Running service without TLS");
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(serviceAddress.Address, serviceAddress.Port);
            });
        }

        // Register services
        builder.Services.AddSingleton(cloudBoardProvider);
        foreach (var provider in providers)
        {
            builder.Services.AddSingleton(provider);
        }

        var app = builder.Build();

        // Map gRPC services
        app.MapGrpcService<CloudBoardGrpcService>();
        app.MapGrpcService<HealthGrpcService>();

        // Start the server
        await app.RunAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        try
        {
            await Task.WhenAll(_runningTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during shutdown");
        }

        _cts.Dispose();
    }
}

public class InitializationException : Exception
{
    public InitializationException(string message) : base(message) { }
}


//public interface IHeartbeatPublisher
//{
//    Task StartAsync(CancellationToken cancellationToken);
//    Task StopAsync(CancellationToken cancellationToken);
//}

//public interface IJobHelperClient
//{
//    Task<JobResponse> HandleJobRequestAsync(JobRequest request, CancellationToken cancellationToken);
//}

//public interface IAttestationClient
//{
//    Task<AttestationResponse> GetAttestationAsync(CancellationToken cancellationToken);
//}

//public interface IJobAuthClient
//{
//    //Implement authentication
//}


//public interface IWorkloadController
//{
//    Task StartAsync(CancellationToken cancellationToken);
//    Task StopAsync(CancellationToken cancellationToken);
//}

//public interface IHotPropertiesController
//{
//    event EventHandler<HotPropertiesUpdatedEventArgs> HotPropertyUpdated; // Define an event for configuration changes
//    Task StartAsync(CancellationToken cancellationToken);
//    Task StopAsync(CancellationToken cancellationToken);
//}


//public class HotPropertiesUpdatedEventArgs : EventArgs
//{
//    public Dictionary<string, string> NewProperties { get; set; }

//    public HotPropertiesUpdatedEventArgs(Dictionary<string, string> newProperties)
//    {
//        NewProperties = newProperties;
//    }
//}


//public interface IGrpcServer
//{
//    Task StartAsync(CancellationToken cancellationToken);
//    Task StopAsync(CancellationToken cancellationToken);
//}
//public class NIOTSEventLoopGroup
//{
//    public void ShutdownGracefully()
//    {
//        // Shutdown the event loop group
//    }
//    public void Dispose()
//    {
//        // Dispose the event loop group
//    }
//}
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class CloudBoardProvider : ICallHandlerProvider
    {
        private readonly IJobHelperClientProvider _jobHelperClientProvider;
        private readonly IHealthPublisher _healthPublisher;
        private readonly IMetricsSystem _metrics;
        private readonly ITracer _tracer;
        private readonly IAttestationProvider _attestationProvider;
        private readonly LoadConfig? _loadConfig;
        private readonly IHotPropertiesController? _hotProperties;
        private readonly ILogger<CloudBoardProvider> _logger;
        private readonly SemaphoreSlim _requestSemaphore;
        private readonly Subject<int> _concurrentRequestCountSubject = new();

        public IObservable<int> ConcurrentRequestCountStream => _concurrentRequestCountSubject;
        public bool Pause { get; set; } = false;

        public CloudBoardProvider(
            IJobHelperClientProvider jobHelperClientProvider,
            IHealthPublisher healthPublisher,
            IMetricsSystem metrics,
            ITracer tracer,
            IAttestationProvider attestationProvider,
            LoadConfig? loadConfig,
            IHotPropertiesController? hotProperties)
        {
            _jobHelperClientProvider = jobHelperClientProvider;
            _healthPublisher = healthPublisher;
            _metrics = metrics;
            _tracer = tracer;
            _attestationProvider = attestationProvider;
            _loadConfig = loadConfig;
            _hotProperties = hotProperties;
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<CloudBoardProvider>();

            int maxConcurrentRequests = loadConfig?.MaxConcurrentRequests ?? 100;
            _requestSemaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);

            // Initial count update
            _concurrentRequestCountSubject.OnNext(0);
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("CloudBoard provider running");

            // Nothing to do here, the provider handles gRPC requests
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        public async Task DrainAsync()
        {
            _logger.LogInformation("Draining CloudBoard provider");

            // Wait for all requests to complete
            for (int i = 0; i < _loadConfig?.MaxConcurrentRequests ?? 100; i++)
            {
                await _requestSemaphore.WaitAsync();
            }
        }

        public async Task<TResponse> HandleRequestAsync<TRequest, TResponse>(
            TRequest request,
            Func<TRequest, Task<TResponse>> handler,
            string operationName,
            CancellationToken cancellationToken)
            where TResponse : class, new()
        {
            if (Pause)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Service is paused"));
            }

            if (_healthPublisher.CurrentStatus != HealthStatus.Serving)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Service is not healthy"));
            }

            var span = _tracer.StartSpan(operationName);
            int currentRequests = 0;

            try
            {
                await _requestSemaphore.WaitAsync(cancellationToken);

                try
                {
                    currentRequests = Interlocked.Increment(ref _currentRequests);
                    _concurrentRequestCountSubject.OnNext(currentRequests);

                    return await handler(request);
                }
                finally
                {
                    currentRequests = Interlocked.Decrement(ref _currentRequests);
                    _concurrentRequestCountSubject.OnNext(currentRequests);
                    _requestSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                span.End(false);

                if (ex is RpcException)
                {
                    throw;
                }

                _logger.LogError(ex, "Error handling request: {OperationName}", operationName);
                throw new RpcException(new Status(StatusCode.Internal, "Internal error"));
            }
            finally
            {
                span.End(true);
            }
        }
    }
}

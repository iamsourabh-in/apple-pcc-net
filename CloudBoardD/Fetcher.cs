using CloudBoardCommon;
using CloudBoardD.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloudBoardD
{
    public class Fetcher : IInfoDelegate
    {
        private readonly SchedulingConfiguration _schedule;
        private readonly FetcherDataSource _dataSource;
        private readonly IMetricsSystem _metrics;
        private readonly ILogger<Fetcher> _logger;
        private IFetcherDelegate? _delegate;
        private DateTime _lastFetchTime = DateTime.MinValue;
        private ConfigurationInfo? _lastFetchedInfo;
        private readonly SemaphoreSlim _fetchLock = new(1, 1);

        public Fetcher(
            SchedulingConfiguration schedule,
            FetcherDataSource dataSource,
            IMetricsSystem metrics)
        {
            _schedule = schedule;
            _dataSource = dataSource;
            _metrics = metrics;
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<Fetcher>();
        }

        public async Task set(delegate: IFetcherDelegate)
        {
            _delegate = delegate;
            
            // If we already have data, provide it to the new delegate
            if (_lastFetchedInfo != null)
            {
                await _delegate.OnConfigurationUpdate(_lastFetchedInfo);
    }


public async Task<ConfigurationInfo?> GetCurrentConfigurationInfoAsync()
        {
            return _lastFetchedInfo;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting fetcher with schedule: {Schedule}", _schedule);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await FetchAndUpdateAsync(cancellationToken);

                    // Calculate next fetch time
                    TimeSpan delay = CalculateNextFetchDelay();
                    _logger.LogDebug("Next fetch in {Delay}", delay);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Normal cancellation
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during fetch operation");

                    // Wait before retrying
                    await Task.Delay(_schedule.ErrorRetryInterval, cancellationToken);
                }
            }
        }

        private async Task FetchAndUpdateAsync(CancellationToken cancellationToken)
        {
            await _fetchLock.WaitAsync(cancellationToken);

            try
            {
                _logger.LogInformation("Fetching configuration");

                var fetchStartTime = DateTime.UtcNow;
                var configInfo = await _dataSource.FetchConfigurationAsync(cancellationToken);
                var fetchDuration = DateTime.UtcNow - fetchStartTime;

                _metrics.EmitRequestMetric("fetch_configuration", fetchDuration, configInfo != null);

                if (configInfo != null)
                {
                    _lastFetchTime = DateTime.UtcNow;
                    _lastFetchedInfo = configInfo;

                    if (_delegate != null)
                    {
                        await _delegate.OnConfigurationUpdate(configInfo);
                    }
                }
                else
                {
                    _logger.LogWarning("Fetch returned null configuration");
                }
            }
            finally
            {
                _fetchLock.Release();
            }
        }

        private TimeSpan CalculateNextFetchDelay()
        {
            if (_lastFetchTime == DateTime.MinValue)
            {
                // First fetch, use initial delay
                return _schedule.InitialFetchDelay;
            }

            // Calculate time since last fetch
            var timeSinceLastFetch = DateTime.UtcNow - _lastFetchTime;

            // If we've exceeded the polling interval, fetch immediately
            if (timeSinceLastFetch >= _schedule.PollingInterval)
            {
                return TimeSpan.Zero;
            }

            // Otherwise, wait for the remaining time
            return _schedule.PollingInterval - timeSinceLastFetch;
        }

        public static FetcherDataSource DataSourceFromConfiguration(
            CloudBoardConfiguration config,
            ILogger logger,
            IMetricsSystem metrics)
        {
            // Create appropriate data source based on configuration
            return new HttpFetcherDataSource(config, logger, metrics);
        }

public class SchedulingConfiguration
    {
        public TimeSpan InitialFetchDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromSeconds(30);

        public SchedulingConfiguration(FetcherScheduleConfig? config = null)
        {
            if (config != null)
            {
                InitialFetchDelay = TimeSpan.FromSeconds(config.InitialFetchDelaySeconds);
                PollingInterval = TimeSpan.FromSeconds(config.PollingIntervalSeconds);
                ErrorRetryInterval = TimeSpan.FromSeconds(config.ErrorRetryIntervalSeconds);
            }
        }
    }
}

public class FetcherScheduleConfig
{
    public double InitialFetchDelaySeconds { get; set; } = 1;
    public double PollingIntervalSeconds { get; set; } = 300;
    public double ErrorRetryIntervalSeconds { get; set; } = 30;
}

public interface IFetcherDelegate
{
    Task OnConfigurationUpdate(ConfigurationInfo configInfo);
}

public interface IInfoDelegate
{
    Task<ConfigurationInfo?> GetCurrentConfigurationInfoAsync();
}

public abstract class FetcherDataSource
{
    public abstract Task<ConfigurationInfo?> FetchConfigurationAsync(CancellationToken cancellationToken = default);
}

public class HttpFetcherDataSource : FetcherDataSource
{
    private readonly CloudBoardConfiguration _config;
    private readonly ILogger _logger;
    private readonly IMetricsSystem _metrics;
    private readonly HttpClient _httpClient;

    public HttpFetcherDataSource(
        CloudBoardConfiguration config,
        ILogger logger,
        IMetricsSystem metrics)
    {
        _config = config;
        _logger = logger;
        _metrics = metrics;
        _httpClient = new HttpClient();
    }

    public override async Task<ConfigurationInfo?> FetchConfigurationAsync(CancellationToken cancellationToken = default)
    {
        // Implement HTTP fetch logic
        try
        {
            // Example implementation
            var response = await _httpClient.GetAsync("https://config-endpoint/config", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<ConfigurationInfo>(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching configuration from HTTP endpoint");
            return null;
        }
    }
}

public class ConfigurationInfo
{
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<ServiceEndpoint> Endpoints { get; set; } = new();
}

public class ServiceEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
}

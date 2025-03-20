using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class Registry : IFetcherDelegate, IServerDelegate
    {
        private readonly IMetricsSystem _metrics;
        private readonly ILogger<Registry> _logger;
        private ConfigurationInfo? _currentConfig;
        private readonly object _lock = new();

        public Registry(IMetricsSystem metrics)
        {
            _metrics = metrics;
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<Registry>();
        }

        public async Task OnConfigurationUpdate(ConfigurationInfo configInfo)
        {
            lock (_lock)
            {
                _logger.LogInformation("Received configuration update: version {Version}", configInfo.Version);
                _currentConfig = configInfo;
            }
        }

        public async Task<ConfigurationInfo?> GetConfigurationAsync()
        {
            lock (_lock)
            {
                return _currentConfig;
            }
        }
    }

    public interface IServerDelegate
    {
        Task<ConfigurationInfo?> GetConfigurationAsync();
    }
}

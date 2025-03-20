using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardD
{
    public class CloudBoardWorker : BackgroundService
    {
        private readonly ILogger<CloudBoardWorker> _logger;
        private CloudBoardDaemon? _daemon;

        public CloudBoardWorker(ILogger<CloudBoardWorker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CloudBoard Worker starting at: {time}", DateTimeOffset.Now);

            try
            {
                // Get config path from environment or command line
                string? configPath = Environment.GetEnvironmentVariable("CLOUDBOARD_CONFIG_PATH");

                // Create and start the daemon
                _daemon = new CloudBoardDaemon(configPath);
                await _daemon.StartAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in CloudBoard Worker");
                Environment.Exit(1);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("CloudBoard Worker stopping at: {time}", DateTimeOffset.Now);

            if (_daemon != null)
            {
                await _daemon.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
     /// <summary>
     /// Interface for providers that handle gRPC calls
     /// </summary>
    public interface ICallHandlerProvider
    {
        Task RunAsync(CancellationToken cancellationToken = default);
    }
    public class HealthProvider : ICallHandlerProvider
    {
        private readonly IHealthPublisher _healthPublisher;

        public HealthProvider(IHealthPublisher healthPublisher)
        {
            _healthPublisher = healthPublisher;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            // Monitor system health and update status
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check system health
                bool isHealthy = await CheckSystemHealthAsync();

                _healthPublisher.UpdateStatus(isHealthy ?
                    HealthStatus.Serving :
                    HealthStatus.NotServing);

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        private async Task<bool> CheckSystemHealthAsync()
        {
            // Implement system health checks
            return true;
        }
    }

    public class HealthServer : IHealthServer
    {
        public async Task RunAsync(IHealthPublisher healthPublisher, CancellationToken cancellationToken = default)
        {
            // Implement health server that exposes health status via HTTP endpoint
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 8080);
            });

            var app = builder.Build();

            app.MapGet("/health", () =>
            {
                return healthPublisher.CurrentStatus switch
                {
                    HealthStatus.Serving => Results.Ok("Healthy"),
                    _ => Results.StatusCode(503)
                };
            });

            await app.RunAsync(cancellationToken);
        }
    }

    public interface IHealthServer
    {
        Task RunAsync(IHealthPublisher healthPublisher, CancellationToken cancellationToken = default);
    }
}

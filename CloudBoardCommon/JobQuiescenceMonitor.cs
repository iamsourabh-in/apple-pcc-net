using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class JobQuiescenceMonitor
    {
        private readonly ILifecycleManager _lifecycleManager;
        private readonly ILogger<JobQuiescenceMonitor> _logger;
        private readonly TaskCompletionSource _quiescenceCompletedTcs = new();

        public JobQuiescenceMonitor(ILifecycleManager lifecycleManager)
        {
            _lifecycleManager = lifecycleManager;
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<JobQuiescenceMonitor>();
        }

        public async Task StartQuiescenceMonitorAsync()
        {
            // Monitor for system signals to start quiescence
            _logger.LogInformation("Starting job quiescence monitor");

            // Set up signal handlers
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _lifecycleManager.StartDrain();
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                _lifecycleManager.StartDrain();
            };
        }

        public Task QuiesceCompletedAsync()
        {
            _logger.LogInformation("Quiescence completed");
            _quiescenceCompletedTcs.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WaitForQuiescenceCompletedAsync()
        {
            return _quiescenceCompletedTcs.Task;
        }
    }
}

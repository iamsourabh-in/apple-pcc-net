using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class LifecycleManagerOptions
    {
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    }

    public class LifecycleManager : ILifecycleManager
    {
        private readonly LifecycleManagerOptions _options;
        private readonly ILogger<LifecycleManager> _logger;
        private readonly CancellationTokenSource _drainCts = new();
        private bool _isDraining = false;
        private readonly object _lock = new();

        public LifecycleManager(LifecycleManagerOptions options)
        {
            _options = options;
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<LifecycleManager>();
        }

        public async Task ManagedAsync(Func<Task> runAction, Func<Task>? onDrain = null)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_drainCts.Token);

            // Handle process termination signals
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                StartDrain();
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                StartDrain();
            };

            try
            {
                // Run the main action
                await runAction();
            }
            catch (OperationCanceledException) when (_drainCts.IsCancellationRequested)
            {
                _logger.LogInformation("Operation canceled due to drain request");
            }
            finally
            {
                // Perform drain operations if needed
                if (_isDraining && onDrain != null)
                {
                    try
                    {
                        var drainTask = onDrain();
                        var timeoutTask = Task.Delay(_options.Timeout);

                        var completedTask = await Task.WhenAny(drainTask, timeoutTask);
                        if (completedTask == timeoutTask)
                        {
                            _logger.LogWarning("Drain operation timed out after {Timeout}", _options.Timeout);
                        }
                        else
                        {
                            await drainTask; // Ensure any exceptions are propagated
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during drain operation");
                    }
                }
            }
        }

        public void StartDrain()
        {
            lock (_lock)
            {
                if (!_isDraining)
                {
                    _isDraining = true;
                    _logger.LogInformation("Starting drain operation");
                    _drainCts.Cancel();
                }
            }
        }

        public bool IsDraining
        {
            get
            {
                lock (_lock)
                {
                    return _isDraining;
                }
            }
        }
    }

    public interface ILifecycleManager
    {
        Task ManagedAsync(Func<Task> runAction, Func<Task>? onDrain = null);
        void StartDrain();
        bool IsDraining { get; }
    }
}

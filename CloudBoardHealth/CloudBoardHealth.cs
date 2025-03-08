using CloudBoardCommon;
namespace CloudBoardHealth;

public class CloudBoardHealthCheck : IHealthCheck
    {
        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
        {
            // Perform health checks here (database connection, etc.)
            // For a simple check, just return true
            await Task.Delay(100, cancellationToken); // Simulate some work
            return true;
        }
    }

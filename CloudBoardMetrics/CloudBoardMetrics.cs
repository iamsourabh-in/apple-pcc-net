using Prometheus.Client;
using Prometheus.Client.Collectors;

namespace CloudBoardMetrics
{
    public class Metrics
    {
        private static readonly MetricFactory _metricFactory = new MetricFactory(new CollectorRegistry());

        public static readonly IGauge CloudBoardUptime = _metricFactory.CreateGauge("cloudboard_uptime", "Uptime of the CloudBoard daemon in seconds");
        public static readonly ICounter JobRequestsTotal = _metricFactory.CreateCounter("job_requests_total", "Total number of job requests processed");
        // Add other metrics as needed...
    }
}

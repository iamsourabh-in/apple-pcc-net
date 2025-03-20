using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CloudBoardD.Core
{
    public class CloudBoardConfiguration
    {
        public GrpcConfig? Grpc { get; set; }
        public HeartbeatConfig? Heartbeat { get; set; }
        public ServiceDiscoveryConfig? ServiceDiscovery { get; set; }
        public LifecycleManagerConfig? LifecycleManager { get; set; }
        public PrewarmingConfig? Prewarming { get; set; }
        public LoadConfig? Load { get; set; }
        public bool BlockHealthinessOnAuthSigningKeysPresence { get; set; } = true;

        public static CloudBoardConfiguration FromFile(string path, ISecureConfigLoader secureConfigLoader)
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<CloudBoardConfiguration>(json)
                ?? throw new InvalidOperationException("Failed to deserialize configuration");

            // Process any secure configuration elements
            secureConfigLoader.LoadSecureConfig(config);

            return config;
        }

        public static CloudBoardConfiguration FromEnvironment()
        {
            // Load configuration from environment variables or app settings
            return new CloudBoardConfiguration();
        }
    }

    public class GrpcConfig
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? ExpectedPeerIdentifier { get; set; }
        public KeepaliveConfig? Keepalive { get; set; }
    }

    public class KeepaliveConfig
    {
        public TimeSpan Interval { get; set; }
        public TimeSpan Timeout { get; set; }
    }

    public class HeartbeatConfig
    {
        public string? Endpoint { get; set; }
        public TimeSpan Interval { get; set; }
    }

    public class ServiceDiscoveryConfig
    {
        public string? Endpoint { get; set; }
        public string? CellID { get; set; }
    }

    public class LifecycleManagerConfig
    {
        public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromMinutes(2);
    }

    public class PrewarmingConfig
    {
        public int PrewarmedPoolSize { get; set; } = 3;
        public int MaxProcessCount { get; set; } = 3;
    }

    public class LoadConfig
    {
        public int MaxConcurrentRequests { get; set; } = 100;
    }

    public interface ISecureConfigLoader
    {
        void LoadSecureConfig(CloudBoardConfiguration config);
    }

    public class RealSecureConfigLoader : ISecureConfigLoader
    {
        public void LoadSecureConfig(CloudBoardConfiguration config)
        {
            // Load secure configuration elements from a secure store
        }
    }
}

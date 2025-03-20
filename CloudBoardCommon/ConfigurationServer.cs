using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudBoardCommon
{
    public class ConfigurationServer
    {
        private readonly ConfigurationAPIXPCServer _xpcServer;
        private readonly ILogger<ConfigurationServer> _logger;
        private IServerDelegate? _delegate;
        private IInfoDelegate? _infoDelegate;

        public ConfigurationServer(ConfigurationAPIXPCServer xpcServer)
        {
            _xpcServer = xpcServer;
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<ConfigurationServer>();
        }

        public async Task set(delegate: IServerDelegate)
         {
            _delegate = delegate;
        }

        public async Task set(infoDelegate: IInfoDelegate)
        {
            _infoDelegate = infoDelegate;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting configuration server");

            _xpcServer.OnGetConfigurationRequest = async () =>
            {
                if (_delegate == null)
                {
                    throw new InvalidOperationException("Server delegate not set");
                }

                return await _delegate.GetConfigurationAsync();
            };

            _xpcServer.OnGetConfigurationInfoRequest = async () =>
            {
                if (_infoDelegate == null)
                {
                    throw new InvalidOperationException("Info delegate not set");
                }

                return await _infoDelegate.GetCurrentConfigurationInfoAsync();
            };

            await _xpcServer.RunAsync(cancellationToken);
        }
    }

    public class ConfigurationAPIXPCServer
{
    private readonly CloudBoardAsyncXPCListener _listener;
    private readonly ILogger<ConfigurationAPIXPCServer> _logger;

    public Func<Task<ConfigurationInfo?>>? OnGetConfigurationRequest { get; set; }
    public Func<Task<ConfigurationInfo?>>? OnGetConfigurationInfoRequest { get; set; }

    public ConfigurationAPIXPCServer(CloudBoardAsyncXPCListener listener)
    {
        _listener = listener;
        _logger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<ConfigurationAPIXPCServer>();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting XPC server");

        await _listener.StartAsync(async (connection, ct) =>
        {
            _logger.LogDebug("Received XPC connection");

            try
            {
                // Process messages from the connection
                await foreach (var message in connection.ReceiveMessagesAsync(ct))
                {
                    if (message.ContainsKey("get_configuration"))
                    {
                        if (OnGetConfigurationRequest == null)
                        {
                            await connection.SendErrorAsync("Handler not set", ct);
                            continue;
                        }

                        var config = await OnGetConfigurationRequest();
                        await connection.SendResponseAsync(new Dictionary<string, object>
                        {
                            ["configuration"] = config ?? new ConfigurationInfo()
                        }, ct);
                    }
                    else if (message.ContainsKey("get_configuration_info"))
                    {
                        if (OnGetConfigurationInfoRequest == null)
                        {
                            await connection.SendErrorAsync("Handler not set", ct);
                            continue;
                        }

                        var configInfo = await OnGetConfigurationInfoRequest();
                        await connection.SendResponseAsync(new Dictionary<string, object>
                        {
                            ["configuration_info"] = configInfo ?? new ConfigurationInfo()
                        }, ct);
                    }
                    else
                    {
                        await connection.SendErrorAsync("Unknown request", ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing XPC connection");
            }
        }, cancellationToken);
    }
}
}

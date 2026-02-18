using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Threading;
    using System.Threading.Tasks;
    using MangaMesh.Peer.Core.Transport; // Added for RoutingEntry

    public class DhtHostedService : IHostedService
    {
        private readonly IDhtNode _dhtNode;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DhtHostedService> _logger;

        public DhtHostedService(IDhtNode dhtNode, IConfiguration configuration, ILogger<DhtHostedService> logger)
        {
            _dhtNode = dhtNode;
            _configuration = configuration;
            _logger = logger;
            Console.WriteLine("[DhtHostedService] Constructor called.");
            _logger.LogInformation("DhtHostedService initialized.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting DhtHostedService...");
            var enableBootstrap = _configuration.GetValue<bool>("Dht:Bootstrap", true);
            var bootstrapNodesConfig = _configuration.GetValue<string>("Dht:BootstrapNodes");
            var bootstrapNodes = new List<RoutingEntry>();

            if (!string.IsNullOrEmpty(bootstrapNodesConfig))
            {
                var nodes = bootstrapNodesConfig.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var node in nodes)
                {
                    var parts = node.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                    {
                        bootstrapNodes.Add(new RoutingEntry
                        {
                            NodeId = Array.Empty<byte>(), // Unknown ID, will discover
                            Address = new NodeAddress(parts[0], port),
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }
                }
                _logger.LogInformation("Parsed {Count} bootstrap nodes from config.", bootstrapNodes.Count);
            }
            else
            {
                _logger.LogInformation("No bootstrap nodes configured.");
            }

            // Start message loop + maintenance
            _dhtNode.StartWithMaintenance(enableBootstrap, bootstrapNodes.Count > 0 ? bootstrapNodes : null);

            _logger.LogInformation("DhtHostedService started.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping DhtHostedService...");
            _dhtNode.StopWithMaintenance();
            return Task.CompletedTask;
        }
    }

}

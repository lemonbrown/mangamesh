using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Threading;
    using System.Threading.Tasks;
    using MangaMesh.Peer.Core.Transport; // Added for RoutingEntry
    using MangaMesh.Peer.Core.Blob;

    public class DhtHostedService : IHostedService
    {
        private readonly IDhtNode _dhtNode;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DhtHostedService> _logger;

        public DhtHostedService(IDhtNode dhtNode, IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DhtHostedService> logger)
        {
            _dhtNode = dhtNode;
            _scopeFactory = scopeFactory;
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

            // Re-announce all on-disk blobs so the gateway can find them after a restart.
            // Runs in background after a short delay to let DHT bootstrap establish connections first.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                using var scope = _scopeFactory.CreateScope();
                var blobStore = scope.ServiceProvider.GetRequiredService<IBlobStore>();
                var hashes = blobStore.GetAllHashes().ToList();
                _logger.LogInformation("Re-announcing {Count} on-disk blobs to DHT after startup.", hashes.Count);
                foreach (var blobHash in hashes)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    try { await _dhtNode.StoreAsync(Convert.FromHexString(blobHash.Value)); }
                    catch { /* best-effort */ }
                }
                _logger.LogInformation("DHT startup blob re-announcement complete.");
            }, cancellationToken);

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

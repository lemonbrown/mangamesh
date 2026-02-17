using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.Core.Node
{
    public sealed class YamlBootstrapNodeProvider : IBootstrapNodeProvider
    {
        private readonly DhtOptions _options;
        private readonly ILogger<YamlBootstrapNodeProvider> _logger;

        public YamlBootstrapNodeProvider(IOptions<DhtOptions> options, ILogger<YamlBootstrapNodeProvider> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public Task<IReadOnlyList<RoutingEntry>> GetBootstrapNodesAsync()
        {
            var nodes = new List<RoutingEntry>();
            var configPath = _options.BootstrapNodesPath
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "bootstrap_nodes.yml");

            if (!File.Exists(configPath))
                return Task.FromResult<IReadOnlyList<RoutingEntry>>(nodes);

            try
            {
                var yaml = File.ReadAllText(configPath);
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                    .Build();

                var configs = deserializer.Deserialize<List<BootstrapNodeConfig>>(yaml);
                if (configs != null)
                {
                    foreach (var c in configs)
                    {
                        nodes.Add(new RoutingEntry
                        {
                            NodeId = Convert.FromHexString(c.NodeId),
                            Address = new NodeAddress(c.Address.Host, c.Address.Port),
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load bootstrap nodes from {Path}", configPath);
            }

            return Task.FromResult<IReadOnlyList<RoutingEntry>>(nodes);
        }
    }
}

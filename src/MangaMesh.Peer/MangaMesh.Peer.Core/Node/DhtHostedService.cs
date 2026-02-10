using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using System.Threading;
    using System.Threading.Tasks;

    public class DhtHostedService : IHostedService
    {
        private readonly IDhtNode _dhtNode;
        private readonly IConfiguration _configuration;

        public DhtHostedService(IDhtNode dhtNode, IConfiguration configuration)
        {
            _dhtNode = dhtNode;
            _configuration = configuration;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var enableBootstrap = _configuration.GetValue<bool>("Dht:Bootstrap", true);
            // Start message loop + maintenance
            _dhtNode.StartWithMaintenance(enableBootstrap);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _dhtNode.StopWithMaintenance();
            return Task.CompletedTask;
        }
    }

}

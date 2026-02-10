using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public class NodeIdentityService : INodeIdentityService
    {
        public string NodeId { get; }
        public bool IsConnected { get; private set; }
        public DateTime? LastPingUtc { get; private set; }


        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NodeIdentityService> _logger;

        public NodeIdentityService(IServiceScopeFactory scopeFactory, ILogger<NodeIdentityService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            NodeId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("Generated in-memory NodeId: {NodeId}", NodeId);
        }

        public void UpdateStatus(bool isConnected)
        {
            IsConnected = isConnected;
            if (isConnected)
            {
                LastPingUtc = DateTime.UtcNow;
            }
        }

    }
}

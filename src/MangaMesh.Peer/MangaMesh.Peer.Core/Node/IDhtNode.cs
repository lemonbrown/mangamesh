using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtNode
    {
        INodeIdentity Identity { get; }
        ITransport Transport { get; }

        Task StoreAsync(byte[] contentHash);
        Task<List<byte[]>> FindValueAsync(byte[] contentHash);
        Task<List<DhtNode.ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash);
        Task<ContentMessage?> SendContentRequestAsync(NodeAddress address, ContentMessage message, TimeSpan timeout);

        // Internal handler for responses
        void HandleContentMessage(ContentMessage message);
        Task HandleMessageAsync(DhtMessage message);
        Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrapNode = null);
        Task PingAsync(RoutingEntry node);
        void StartWithMaintenance(bool enableBootstrap = true, List<RoutingEntry>? bootstrapNodes = null);
        void StopWithMaintenance();
    }
}

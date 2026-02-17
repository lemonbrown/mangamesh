using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IRoutingTable
    {
        void AddOrUpdate(RoutingEntry entry);
        IReadOnlyList<RoutingEntry> FindClosest(byte[] targetId, int k = 20);
        IReadOnlyList<RoutingEntry> GetAll();
        NodeAddress? GetAddressForNode(byte[] nodeId);
        int BucketCount { get; }
    }
}

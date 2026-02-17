using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IBootstrapNodeProvider
    {
        Task<IReadOnlyList<RoutingEntry>> GetBootstrapNodesAsync();
    }
}

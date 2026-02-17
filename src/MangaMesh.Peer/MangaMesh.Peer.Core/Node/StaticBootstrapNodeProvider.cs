using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    /// <summary>Bootstrap node provider backed by a fixed list — used in tests.</summary>
    public sealed class StaticBootstrapNodeProvider : IBootstrapNodeProvider
    {
        private readonly IReadOnlyList<RoutingEntry> _nodes;

        public StaticBootstrapNodeProvider(IEnumerable<RoutingEntry> nodes)
        {
            _nodes = nodes.ToList();
        }

        public Task<IReadOnlyList<RoutingEntry>> GetBootstrapNodesAsync()
            => Task.FromResult(_nodes);
    }
}

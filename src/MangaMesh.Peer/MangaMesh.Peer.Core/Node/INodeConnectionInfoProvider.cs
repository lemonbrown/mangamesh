using System;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public interface INodeConnectionInfoProvider
    {
        /// <summary>
        /// Returns the node's IP, DHT TCP port, and HTTP API port.
        /// DhtPort or HttpApiPort may be 0 if not applicable for this node type.
        /// </summary>
        Task<(string IP, int DhtPort, int HttpApiPort)> GetConnectionInfoAsync();
    }
}

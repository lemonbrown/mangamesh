using MangaMesh.Peer.ClientApi.Models;

namespace MangaMesh.Peer.ClientApi.Services
{
    public interface INodeStatusService
    {
        /// <summary>
        /// Get the current status of this node.
        /// </summary>
        /// <returns>Node status DTO</returns>
        Task<NodeStatusDto> GetStatusAsync(CancellationToken ct = default);
    }

}

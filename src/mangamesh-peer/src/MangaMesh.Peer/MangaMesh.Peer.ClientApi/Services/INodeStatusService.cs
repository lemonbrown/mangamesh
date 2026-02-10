using MangaMesh.Server.Models;

namespace MangaMesh.Server.Services
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

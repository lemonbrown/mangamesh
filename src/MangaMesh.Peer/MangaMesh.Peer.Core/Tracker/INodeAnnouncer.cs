using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>Node-level sync — announcing presence and checking node existence.</summary>
    public interface INodeAnnouncer
    {
        Task<bool> PingAsync(string nodeId, string manifestSetHash, int manifestCount);
        Task AnnounceAsync(Shared.Models.AnnounceRequest announceRequest);
        Task<bool> CheckNodeExistsAsync(string nodeId);
    }
}

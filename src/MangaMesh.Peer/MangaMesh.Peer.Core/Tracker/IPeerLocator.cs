using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>Peer discovery — read-only queries for which nodes host a given manifest.</summary>
    public interface IPeerLocator
    {
        Task<List<PeerInfo>> GetPeersForManifestAsync(string manifestHash);
        Task<PeerInfo?> GetPeerAsync(string seriesId, string chapterId, string manifestHash);
    }
}

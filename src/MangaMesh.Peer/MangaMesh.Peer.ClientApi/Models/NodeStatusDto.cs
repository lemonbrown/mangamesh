namespace MangaMesh.Peer.ClientApi.Models
{
    public record NodeStatusDto(
        string NodeId,
        int PeerCount,
        int SeededManifests,
        long StorageUsedMb
    );

}

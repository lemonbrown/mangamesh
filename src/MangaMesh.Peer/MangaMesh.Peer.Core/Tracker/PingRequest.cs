namespace MangaMesh.Peer.Core.Tracker
{
    public record PingRequest(
        string NodeId,
        string ManifestSetHash,
        int ManifestCount
    );
}

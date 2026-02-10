namespace MangaMesh.Client.Tracker
{
    public record PingRequest(
        string NodeId,
        string IP,
        int Port,
        string ManifestSetHash,
        int ManifestCount
    );
}

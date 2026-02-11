namespace MangaMesh.Shared.Models
{
    public record PingRequest(
        string NodeId,
        string ManifestSetHash,
        int ManifestCount,
        string Version = "1.0.0"
    );
}

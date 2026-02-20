namespace MangaMesh.Shared.Models
{
    public record AnnounceRequest(
        string NodeId,
        List<string> Manifests,
        List<ManifestSummary>? ManifestData = null
    );
}

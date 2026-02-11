namespace MangaMesh.Shared.Models
{
    public record AnnounceRequest(
     string NodeId,
     List<string> Manifests
 );
}

namespace MangaMesh.Shared.Models
{
    public record AnnounceRequest(
     string NodeId,
     string IP,
     int Port,
     List<string> Manifests
 );
}

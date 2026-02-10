namespace MangaMesh.Peer.ClientApi.Models
{
    public record ImportResultDto(
        string ManifestHash,
        int FilesImported
    );

}

namespace MangaMesh.Peer.ClientApi.Models
{
    public record ImportUploadResultDto(
        string SourcePath,
        string? ManifestHash,
        bool Success,
        string? ErrorMessage
    );
}

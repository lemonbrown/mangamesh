namespace MangaMesh.Server.Models
{
    public record ImportUploadResultDto(
        string SourcePath,
        string? ManifestHash,
        bool Success,
        string? ErrorMessage
    );
}

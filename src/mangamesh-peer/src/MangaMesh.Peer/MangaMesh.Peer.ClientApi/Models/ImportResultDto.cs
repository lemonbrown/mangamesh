namespace MangaMesh.Server.Models
{
    public record ImportResultDto(
        string ManifestHash,
        int FilesImported
    );

}

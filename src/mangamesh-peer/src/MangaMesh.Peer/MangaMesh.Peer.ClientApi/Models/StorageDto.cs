namespace MangaMesh.Server.Models
{
    public record StorageDto(
        long TotalMb,
        long UsedMb,
        int ManifestCount
    );

}

namespace MangaMesh.Peer.ClientApi.Models
{
    public record StorageDto(
        long TotalMb,
        long UsedMb,
        int ManifestCount
    );

}

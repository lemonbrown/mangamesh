namespace MangaMesh.Peer.ClientApi.Models
{
    public record StoredManifestDto(
        string Hash,
        string SeriesId,
        string ChapterNumber,
        string? Volume,
        string Language,
        string ScanGroup,
        string Title,
        long SizeBytes,
        int FileCount,
        System.DateTime CreatedUtc
    );
}

namespace MangaMesh.Peer.ClientApi.Models
{
    public record ImportChapterRequestDto(
        string SeriesId,
        string ScanlatorId,
        string Language,
        double ChapterNumber,
        string SourcePath,
        string DisplayName,
        string ReleaseType,
        Shared.Models.ExternalMetadataSource Source,
        string ExternalMangaId,
        string Quality = "Unknown"
    );

}

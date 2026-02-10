namespace MangaMesh.Server.Models
{
    public record ImportChapterRequestDto(
        string SeriesId,
        string ScanlatorId,
        string Language,
        double ChapterNumber,
        string SourcePath,
        string DisplayName,
        string ReleaseType,
        MangaMesh.Shared.Models.ExternalMetadataSource Source,
        string ExternalMangaId
    );

}

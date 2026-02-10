namespace MangaMesh.Shared.Models;

public record SeriesSearchResult(string Title, string SeriesId, ExternalMetadataSource Source, string ExternalMangaId, int ChapterCount, DateTime LastUploadedAt);

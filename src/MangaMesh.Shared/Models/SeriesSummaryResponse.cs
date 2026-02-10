using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models;

public class SeriesSummaryResponse
{
    [JsonPropertyName("seriesId")]
    public required string SeriesId { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("source")]
    public required int Source { get; set; }

    [JsonPropertyName("externalMangaId")]
    public required string ExternalMangaId { get; set; }

    [JsonPropertyName("chapterCount")]
    public int ChapterCount { get; set; }

    [JsonPropertyName("seedCount")]
    public int SeedCount { get; set; }

    [JsonPropertyName("lastUploadedAt")]
    public DateTime LastUploadedAt { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("latestChapterNumber")]
    public double? LatestChapterNumber { get; set; }

    [JsonPropertyName("latestChapterTitle")]
    public string? LatestChapterTitle { get; set; }
}

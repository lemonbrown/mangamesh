using System.Text.Json.Serialization;

namespace MangaMesh.Client.Series;

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

    [JsonPropertyName("lastUploadedAt")]
    public DateTime LastUploadedAt { get; set; }
}

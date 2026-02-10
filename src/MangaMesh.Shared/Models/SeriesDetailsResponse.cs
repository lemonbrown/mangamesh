using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models;

public class SeriesDetailsResponse
{
    [JsonPropertyName("seriesId")]
    public required string SeriesId { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("firstSeenUtc")]
    public DateTime? FirstSeenUtc { get; set; }

    [JsonPropertyName("seedCount")]
    public int SeedCount { get; set; }
}

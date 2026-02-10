using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models;

public class ChapterDetailsResponse
{
    [JsonPropertyName("chapterId")]
    public required string ChapterId { get; set; }

    [JsonPropertyName("chapterNumber")]
    public required double ChapterNumber { get; set; }

    [JsonPropertyName("manifests")]
    public IEnumerable<ManifestSummaryResponse>? Manifests { get; set; }
    public string? Title { get; set; }
}

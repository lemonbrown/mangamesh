using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models;

public class ChapterSummaryResponse
{
    [JsonPropertyName("chapterId")]
    public required string ChapterId { get; set; }

    [JsonPropertyName("chapterNumber")]
    public required double ChapterNumber { get; set; }

    [JsonPropertyName("volume")]
    public string? Volume { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("uploadedAt")]
    public DateTime? UploadedAt { get; set; }
}

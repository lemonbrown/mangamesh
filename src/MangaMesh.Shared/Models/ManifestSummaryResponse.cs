using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models;

public class ManifestSummaryResponse
{
    [JsonPropertyName("manifestHash")]
    public required string ManifestHash { get; set; }

    [JsonPropertyName("language")]
    public required string Language { get; set; }

    [JsonPropertyName("scanGroup")]
    public required string ScanGroup { get; set; }

    [JsonPropertyName("quality")]
    public required string Quality { get; set; }

    [JsonPropertyName("uploadedAt")]
    public DateTime? UploadedAt { get; set; }
}


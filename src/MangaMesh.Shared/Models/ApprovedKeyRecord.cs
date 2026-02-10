using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models
{
    public sealed class ApprovedKeyRecord
    {
        [JsonPropertyName("publicKeyBase64")]
        public string PublicKeyBase64 { get; set; } = "";

        [JsonPropertyName("comment")]
        public string Comment { get; set; } = "";

        [JsonPropertyName("addedAt")]
        public DateTimeOffset AddedAt { get; set; }
    }
}

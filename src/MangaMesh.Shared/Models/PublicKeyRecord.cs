using System.Text.Json.Serialization;

namespace MangaMesh.Shared.Models
{
    public sealed class PublicKeyRecord
    {
        [JsonPropertyName("publicKeyBase64")]
        public string PublicKeyBase64 { get; set; } = "";

        [JsonPropertyName("registeredAt")]
        public DateTimeOffset RegisteredAt { get; init; }

        [JsonPropertyName("revoked")]
        public bool Revoked { get; set; }

    }
}

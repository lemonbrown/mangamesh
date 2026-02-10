namespace MangaMesh.Index.Api.Models
{
    public sealed class KeyChallengeResponse
    {
        public string ChallengeId { get; init; } = null!;
        public string Nonce { get; init; } = null!;
        public DateTimeOffset ExpiresAt { get; init; }
    }

}

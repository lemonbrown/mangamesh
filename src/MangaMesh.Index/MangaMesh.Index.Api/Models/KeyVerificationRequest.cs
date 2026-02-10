namespace MangaMesh.Index.Api.Models
{
    public sealed class KeyVerificationRequest
    {
        public string ChallengeId { get; init; } = null!;
        public string SignatureBase64 { get; init; } = null!;
    }

}

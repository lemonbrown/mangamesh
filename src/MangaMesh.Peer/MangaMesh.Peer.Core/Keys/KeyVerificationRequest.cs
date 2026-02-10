namespace MangaMesh.Peer.Core.Keys
{
    public class KeyVerificationRequest
    {
        public string ChallengeId { get; set; } = "";
        public string SignatureBase64 { get; set; } = "";
    }
}

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyChallengeResponse
    {
        public string ChallengeId { get; set; } = "";
        public string Nonce { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }
}

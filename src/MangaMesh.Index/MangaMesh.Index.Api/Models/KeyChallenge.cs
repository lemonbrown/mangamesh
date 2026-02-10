namespace MangaMesh.Index.Api.Models
{
    public class KeyChallenge
    {
        public string Id { get; set; } = default!;
        public string UserId { get; set; } = default!;
        public string Nonce { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
    }


}

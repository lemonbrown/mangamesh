using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Shared.Data.Entities
{
    public class IndexChallengeEntity
    {
        [Key]
        public string Id { get; set; } = default!;

        public string UserId { get; set; } = default!;

        public string Nonce { get; set; } = default!;

        public DateTime ExpiresAt { get; set; }
    }
}

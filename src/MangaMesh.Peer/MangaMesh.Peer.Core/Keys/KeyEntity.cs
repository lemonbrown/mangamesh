using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyEntity
    {
        [Key]
        public int Id { get; set; }
        public string PublicKey { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

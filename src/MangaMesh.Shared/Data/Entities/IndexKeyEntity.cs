using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Shared.Data.Entities
{
    public class IndexKeyEntity
    {
        [Key]
        public string PublicKeyBase64 { get; set; } = default!;

        public DateTimeOffset RegisteredAt { get; set; }

        public bool Revoked { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Shared.Data.Entities
{
    public class IndexApprovedKeyEntity
    {
        [Key]
        public string PublicKeyBase64 { get; set; } = "";
        public string Comment { get; set; } = "";
        public DateTime AddedAt { get; set; }
    }
}

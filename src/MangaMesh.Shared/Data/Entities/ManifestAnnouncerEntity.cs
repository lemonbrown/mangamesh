using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Shared.Data.Entities
{
    public class ManifestAnnouncerEntity
    {
        [Key]
        public int Id { get; set; }

        public string ManifestHash { get; set; } = default!;
        public string NodeId { get; set; } = default!;
        public DateTime AnnouncedAt { get; set; }
    }
}

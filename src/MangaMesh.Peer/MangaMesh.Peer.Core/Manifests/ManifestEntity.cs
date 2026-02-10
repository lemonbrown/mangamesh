using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Peer.Core.Manifests
{
    public class ManifestEntity
    {
        [Key]
        public string Hash { get; set; } = default!;

        [Required]
        public string SeriesId { get; set; } = default!;

        [Required]
        public string ChapterId { get; set; } = default!;

        [Required]
        public string DataJson { get; set; } = default!;

        public DateTime CreatedUtc { get; set; }
    }
}

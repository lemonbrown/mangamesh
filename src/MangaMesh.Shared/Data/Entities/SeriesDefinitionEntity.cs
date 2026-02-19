using System.ComponentModel.DataAnnotations;

namespace MangaMesh.Shared.Data.Entities
{
    public class SeriesDefinitionEntity
    {
        [Key]
        public string SeriesId { get; set; } = default!;

        public int Source { get; set; }
        public string ExternalMangaId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public DateTime CreatedUtc { get; set; }
    }
}

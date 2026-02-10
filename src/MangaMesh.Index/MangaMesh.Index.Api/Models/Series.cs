namespace MangaMesh.Index.Api.Models
{
    public class Series
    {
        public required string SeriesId { get; set; }
        public required string Title { get; set; }
        public Shared.Models.ExternalMetadataSource Source { get; set; }
        public string ExternalMangaId { get; set; } = "";
        public string? Author { get; set; }
        public DateTime? FirstSeenUtc { get; set; }
        public IEnumerable<Chapter>? Chapters { get; set; }
    }

}

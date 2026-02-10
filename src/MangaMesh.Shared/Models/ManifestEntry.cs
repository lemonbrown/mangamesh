namespace MangaMesh.Shared.Models
{
    public class ManifestEntry
    {
        public string ManifestHash { get; set; } = default!;
        public string ExternalMetadataSource { get; set; } = default!;
        public string ExteralMetadataMangaId { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string SeriesId { get; set; } = default!;
        public string ChapterId { get; set; } = default!;
        public double ChapterNumber { get; set; } = default!;
        public string? Volume { get; set; }

        public string Language { get; set; } = default!;
        public string? ScanGroup { get; set; }
        public string? Quality { get; set; }

        public DateTime AnnouncedUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}

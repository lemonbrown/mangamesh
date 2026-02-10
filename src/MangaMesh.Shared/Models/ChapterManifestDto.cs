namespace MangaMesh.Shared.Models
{
    public class ChapterManifestDto
    {
        public string ManifestHash { get; set; } = default!;
        public string Language { get; set; } = default!;
        public string ScanGroup { get; set; } = default!;
        public string Quality { get; set; } = default!;
        public DateTime UploadedAt { get; set; }
    }
}

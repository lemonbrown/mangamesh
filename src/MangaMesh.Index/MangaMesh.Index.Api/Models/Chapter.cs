using MangaMesh.Shared.Models;

namespace MangaMesh.Index.Api.Models
{
    public class Chapter
    {
        public required string ChapterId { get; set; }
        public required double Number { get; set; }
        public string? Volume { get; set; }
        public string? Title { get; set; }
        public IEnumerable<ChapterManifestDto>? Manifests { get; set; }
    }

}

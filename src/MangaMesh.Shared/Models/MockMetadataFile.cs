namespace MangaMesh.Shared.Models
{
    public sealed class MockMetadataFile
    {
        public List<MangaMetadata> Manga { get; init; } = new();
        public List<ChapterMetadata> Chapters { get; init; } = new();
    }

}

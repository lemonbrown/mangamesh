namespace MangaMesh.Shared.Models
{
    public sealed class ChapterMetadata
    {
        public ExternalMetadataSource Source { get; init; }
        public string ExternalChapterId { get; init; } = null!;
        public string ExternalMangaId { get; init; } = null!;

        public string? ChapterNumber { get; init; }
        public string? Volume { get; init; }
        public string? Title { get; init; }

        public string Language { get; init; } = "en";
        public DateTimeOffset? PublishDate { get; init; }
    }

}

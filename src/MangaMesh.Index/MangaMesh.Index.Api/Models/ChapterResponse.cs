using MangaMesh.Shared.Models;

namespace MangaMesh.Index.Api.Models
{
    public sealed class ChapterResponse
    {
        public string ChapterId { get; init; } = null!;
        public string ExternalMangaId { get; init; } = null!;

        public ExternalMetadataSource Source { get; init; }

        public string? ChapterNumber { get; init; }
        public string? Volume { get; init; }
        public string? Title { get; init; }

        public string Language { get; init; } = "en";
        public DateTimeOffset? PublishDate { get; init; }
    }

}

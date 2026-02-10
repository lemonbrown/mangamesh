using MangaMesh.Shared.Models;

namespace MangaMesh.Index.Api.Models
{
    public sealed class ChapterListResponse
    {
        public string MangaId { get; init; } = null!;
        public ExternalMetadataSource Source { get; init; }

        public string Language { get; init; } = "en";

        public IReadOnlyList<ChapterResponse> Chapters { get; init; }
            = Array.Empty<ChapterResponse>();
    }

}

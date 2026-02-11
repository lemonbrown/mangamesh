namespace MangaMesh.Shared.Models
{
    public sealed class MangaSearchResult
    {
        public ExternalMetadataSource Source { get; init; }
        public string ExternalMangaId { get; init; } = null!;

        public string Title { get; init; } = null!;
        public IReadOnlyList<string> AltTitles { get; init; } = Array.Empty<string>();

        public string? Status { get; init; }
        public int? Year { get; init; }
        public string? CoverFilename { get; init; }
    }
}

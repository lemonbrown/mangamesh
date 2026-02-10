namespace MangaMesh.Shared.Models
{
    public sealed class MangaMetadata
    {
        public ExternalMetadataSource Source { get; init; }
        public string ExternalMangaId { get; init; } = null!;

        public string CanonicalTitle { get; init; } = null!;
        public IReadOnlyList<string> AltTitles { get; init; } = Array.Empty<string>();

        public string? Description { get; init; }
        public string? Status { get; init; }   // ongoing, completed, hiatus
        public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
    }
}

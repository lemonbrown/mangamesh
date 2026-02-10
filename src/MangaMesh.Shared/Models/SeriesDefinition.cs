namespace MangaMesh.Shared.Models
{
    public sealed class SeriesDefinition
    {
        public required string SeriesId { get; init; }
        public required ExternalMetadataSource Source { get; init; }
        public required string ExternalMangaId { get; init; }
        public required string Title { get; init; }
        public DateTime CreatedUtc { get; init; }
    }
}

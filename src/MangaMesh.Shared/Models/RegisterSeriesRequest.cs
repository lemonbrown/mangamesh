namespace MangaMesh.Shared.Models
{
    public class RegisterSeriesRequest
    {
        public ExternalMetadataSource Source { get; init; }
        public string ExternalMangaId { get; init; } = "";
    }

    public class RegisterSeriesResponse
    {
        public string SeriesId { get; init; } = "";
        public string Title { get; init; } = "";
    }
}

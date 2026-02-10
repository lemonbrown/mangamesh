namespace MangaMesh.Shared.Models.MangaDex
{
    public sealed class MangaDexSearchResponse
    {
        public string Result { get; init; } = null!;
        public IReadOnlyList<MangaDexMangaData> Data { get; init; } = Array.Empty<MangaDexMangaData>();
    }

}

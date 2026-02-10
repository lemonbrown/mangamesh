namespace MangaMesh.Shared.Models.MangaDex
{
    public sealed class MangaDexMangaResponse
    {
        public string Result { get; init; } = null!;
        public MangaDexMangaData Data { get; init; } = null!;
    }

}

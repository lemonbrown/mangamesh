namespace MangaMesh.Shared.Models.MangaDex
{
    public sealed class MangaDexChapterListResponse
    {
        public string Result { get; init; } = null!;
        public IReadOnlyList<MangaDexChapterData> Data { get; init; } = Array.Empty<MangaDexChapterData>();
    }
}

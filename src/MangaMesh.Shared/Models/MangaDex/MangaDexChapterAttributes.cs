namespace MangaMesh.Shared.Models.MangaDex
{
    public sealed class MangaDexChapterAttributes
    {
        public string? Chapter { get; init; }
        public string? Volume { get; init; }
        public string? Title { get; init; }
        public string TranslatedLanguage { get; init; } = null!;
        public DateTimeOffset? PublishAt { get; init; }
    }
}

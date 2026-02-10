namespace MangaMesh.Shared.Models.MangaDex
{
    public sealed class MangaDexAttributes
    {
        public Dictionary<string, string> Title { get; init; } = new();
        public List<Dictionary<string, string>> AltTitles { get; init; } = new();

        public Dictionary<string, string>? Description { get; init; }
        public string? Status { get; init; }
    }
}

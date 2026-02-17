namespace MangaMesh.SeriesScraper.Models;

public sealed class ChapterInfo
{
    public required string Identifier { get; init; }
    public required double ChapterNumber { get; init; }
    public double? SubChapterNumber { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? PublishDate { get; init; }
    public required string Url { get; init; }
}

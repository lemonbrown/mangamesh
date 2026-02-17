namespace MangaMesh.SeriesScraper.Models;

public sealed class ChapterDownloadResult
{
    public required string ChapterPath { get; init; }
    public required int ImageCount { get; init; }
    public required IReadOnlyList<DownloadedImage> DownloadedImages { get; init; }
}

public sealed class DownloadedImage
{
    public required string Url { get; init; }
    public required string LocalPath { get; init; }
    public required int Index { get; init; }
    public required long FileSize { get; init; }
}

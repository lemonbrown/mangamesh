namespace MangaMesh.SeriesScraper.Core;

public interface IMangaChapterScraper
{
    string SiteName { get; }
    string BaseUrl { get; }

    bool CanHandle(string url);

    Task<IReadOnlyList<Models.ChapterInfo>> GetAvailableChaptersAsync(string seriesSlug);

    Task<IReadOnlyList<string>> GetChapterImageUrlsAsync(string seriesSlug, string chapterIdentifier);

    Task<Stream> DownloadImageAsync(string imageUrl);
}

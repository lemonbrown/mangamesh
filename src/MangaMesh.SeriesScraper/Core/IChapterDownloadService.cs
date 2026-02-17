using MangaMesh.SeriesScraper.Models;

namespace MangaMesh.SeriesScraper.Core;

public interface IChapterDownloadService
{
    Task<ChapterDownloadResult> DownloadChapterAsync(
        IMangaChapterScraper scraper,
        string seriesSlug,
        ChapterInfo chapterInfo,
        string outputBaseDirectory,
        CancellationToken cancellationToken = default);
}

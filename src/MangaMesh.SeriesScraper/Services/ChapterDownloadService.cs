using MangaMesh.SeriesScraper.Core;
using MangaMesh.SeriesScraper.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace MangaMesh.SeriesScraper.Services;

public class ChapterDownloadService : IChapterDownloadService
{
    private readonly ILogger<ChapterDownloadService> _logger;

    public ChapterDownloadService(ILogger<ChapterDownloadService> logger)
    {
        _logger = logger;
    }

    public async Task<ChapterDownloadResult> DownloadChapterAsync(
        IMangaChapterScraper scraper,
        string seriesSlug,
        ChapterInfo chapterInfo,
        string outputBaseDirectory,
        CancellationToken cancellationToken = default)
    {
        var folderName = FormatChapterFolderName(seriesSlug, chapterInfo);
        var chapterPath = Path.Combine(outputBaseDirectory, folderName);

        Directory.CreateDirectory(chapterPath);
        _logger.LogInformation("Created chapter directory: {ChapterPath}", chapterPath);

        var imageUrls = await scraper.GetChapterImageUrlsAsync(seriesSlug, chapterInfo.Identifier);
        _logger.LogInformation("Found {ImageCount} images for chapter {Chapter}", imageUrls.Count, chapterInfo.ChapterNumber);

        var downloadedImages = new List<DownloadedImage>();

        for (int i = 0; i < imageUrls.Count; i++)
        {
            var imageUrl = imageUrls[i];
            var fileName = $"{(i + 1):D4}.png";
            var filePath = Path.Combine(chapterPath, fileName);

            await RetryAsync(async () =>
            {
                _logger.LogDebug("Downloading image {Index}/{Total}: {Url}", i + 1, imageUrls.Count, imageUrl);

                using var imageStream = await scraper.DownloadImageAsync(imageUrl);
                using var image = await Image.LoadAsync(imageStream, cancellationToken);

                await image.SaveAsPngAsync(filePath, new PngEncoder(), cancellationToken);

                var fileInfo = new FileInfo(filePath);
                downloadedImages.Add(new DownloadedImage
                {
                    Url = imageUrl,
                    LocalPath = filePath,
                    Index = i,
                    FileSize = fileInfo.Length
                });

                _logger.LogDebug("Saved image {Index}/{Total} to {FilePath}", i + 1, imageUrls.Count, fileName);
            }, maxRetries: 3, cancellationToken);

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        _logger.LogInformation("Successfully downloaded {Count} images to {Path}", downloadedImages.Count, chapterPath);

        return new ChapterDownloadResult
        {
            ChapterPath = chapterPath,
            ImageCount = downloadedImages.Count,
            DownloadedImages = downloadedImages
        };
    }

    private string FormatChapterFolderName(string seriesSlug, ChapterInfo chapter)
    {
        if (chapter.SubChapterNumber.HasValue)
        {
            return $"{seriesSlug}_{chapter.ChapterNumber}_{chapter.SubChapterNumber}";
        }
        return $"{seriesSlug}_{chapter.ChapterNumber}";
    }

    private async Task RetryAsync(
        Func<Task> operation,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Retry attempt {Attempt}/{MaxRetries} after {Delay}s",
                    attempt + 1, maxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}

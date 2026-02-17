using MangaMesh.SeriesScraper.Core;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace MangaMesh.SeriesScraper.Services;

public class ChapterArchiveService : IChapterArchiveService
{
    private readonly ILogger<ChapterArchiveService> _logger;

    public ChapterArchiveService(ILogger<ChapterArchiveService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ZipChapterAsync(string chapterPath, string outputDirectory)
    {
        var folderName = Path.GetFileName(chapterPath);
        var zipPath = Path.Combine(outputDirectory, $"{folderName}.cbz");

        if (File.Exists(zipPath))
        {
            _logger.LogWarning("Zip file already exists, deleting: {ZipPath}", zipPath);
            File.Delete(zipPath);
        }

        _logger.LogInformation("Creating zip archive: {ZipPath}", zipPath);

        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(
                chapterPath,
                zipPath,
                CompressionLevel.Optimal,
                includeBaseDirectory: false);
        });

        _logger.LogInformation("Successfully created zip archive: {ZipPath}", zipPath);

        return zipPath;
    }

    public Task CleanupSourceDirectory(string chapterPath, bool keepOriginal = false)
    {
        if (!keepOriginal && Directory.Exists(chapterPath))
        {
            _logger.LogInformation("Cleaning up source directory: {ChapterPath}", chapterPath);
            Directory.Delete(chapterPath, recursive: true);
        }
        return Task.CompletedTask;
    }
}

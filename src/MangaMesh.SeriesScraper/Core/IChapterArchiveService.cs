namespace MangaMesh.SeriesScraper.Core;

public interface IChapterArchiveService
{
    Task<string> ZipChapterAsync(string chapterPath, string outputDirectory);
    Task CleanupSourceDirectory(string chapterPath, bool keepOriginal = false);
}

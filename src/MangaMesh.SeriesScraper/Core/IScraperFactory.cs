namespace MangaMesh.SeriesScraper.Core;

public interface IScraperFactory
{
    IMangaChapterScraper GetScraper(string url);
    IMangaChapterScraper GetScraperByName(string siteName);
    IReadOnlyList<string> GetSupportedSites();
}

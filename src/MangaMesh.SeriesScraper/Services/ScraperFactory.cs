using MangaMesh.SeriesScraper.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MangaMesh.SeriesScraper.Services;

public class ScraperFactory : IScraperFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IMangaChapterScraper> _scrapers;

    public ScraperFactory(IServiceProvider serviceProvider, IEnumerable<IMangaChapterScraper> scrapers)
    {
        _serviceProvider = serviceProvider;
        _scrapers = scrapers;
    }

    public IMangaChapterScraper GetScraper(string url)
    {
        var scraper = _scrapers.FirstOrDefault(s => s.CanHandle(url));
        if (scraper == null)
        {
            throw new NotSupportedException($"No scraper found that can handle URL: {url}");
        }
        return scraper;
    }

    public IMangaChapterScraper GetScraperByName(string siteName)
    {
        var scraper = _scrapers.FirstOrDefault(s =>
            s.SiteName.Equals(siteName, StringComparison.OrdinalIgnoreCase));
        if (scraper == null)
        {
            throw new NotSupportedException($"No scraper found for site: {siteName}");
        }
        return scraper;
    }

    public IReadOnlyList<string> GetSupportedSites()
    {
        return _scrapers.Select(s => s.SiteName).ToList();
    }
}

using AngleSharp;
using MangaMesh.SeriesScraper.Core;
using MangaMesh.SeriesScraper.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MangaMesh.SeriesScraper.Sites;

public partial class TcbScansOneChapterScraper : IMangaChapterScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TcbScansOneChapterScraper> _logger;
    private readonly IBrowsingContext _browsingContext;

    public string SiteName => "TCBScansOnePiece";
    public string BaseUrl => "https://tcbscansonepiecechapter.com";

    public TcbScansOneChapterScraper(HttpClient httpClient, ILogger<TcbScansOneChapterScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var config = Configuration.Default;
        _browsingContext = BrowsingContext.New(config);
    }

    public bool CanHandle(string url)
    {
        return url.Contains("tcbscansonepiecechapter.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ChapterInfo>> GetAvailableChaptersAsync(string seriesSlug)
    {
        var url = $"{BaseUrl}/manga/{seriesSlug}/";
        _logger.LogInformation("Fetching chapters from: {Url}", url);

        var html = await FetchPageAsync(url);
        var document = await _browsingContext.OpenAsync(req => req.Content(html));

        // Actual selector: #chapterlist li, with links in .eph-num a
        var liElements = document.QuerySelectorAll("#chapterlist li");
        var chapters = new List<ChapterInfo>();

        foreach (var li in liElements)
        {
            try
            {
                var linkElement = li.QuerySelector(".eph-num a");
                if (linkElement == null) continue;

                var chapterUrl = linkElement.GetAttribute("href");
                if (string.IsNullOrEmpty(chapterUrl)) continue;

                var chapterNumText = li.QuerySelector(".chapternum")?.TextContent?.Trim();
                if (string.IsNullOrEmpty(chapterNumText)) continue;

                var chapterDateText = li.QuerySelector(".chapterdate")?.TextContent?.Trim();

                var (mainNumber, subNumber) = ParseChapterNumber(chapterNumText);
                if (mainNumber == null) continue;

                // Identifier is the URL slug, e.g. "one-piece-chapter-1175"
                var identifier = chapterUrl.TrimEnd('/').Split('/').Last();

                DateTimeOffset? publishDate = null;
                if (!string.IsNullOrEmpty(chapterDateText) &&
                    DateTimeOffset.TryParse(chapterDateText, out var parsedDate))
                {
                    publishDate = parsedDate;
                }

                chapters.Add(new ChapterInfo
                {
                    Identifier = identifier,
                    ChapterNumber = mainNumber.Value,
                    SubChapterNumber = subNumber,
                    Title = null,
                    Url = chapterUrl,
                    PublishDate = publishDate
                });

                _logger.LogDebug("Found chapter: {ChapterNumber} at {Url}", mainNumber.Value, chapterUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse chapter element");
            }
        }

        _logger.LogInformation("Found {Count} chapters", chapters.Count);
        return chapters;
    }

    public async Task<IReadOnlyList<string>> GetChapterImageUrlsAsync(string seriesSlug, string chapterIdentifier)
    {
        // chapterIdentifier is the URL slug, e.g. "one-piece-chapter-1175"
        var url = $"{BaseUrl}/{chapterIdentifier}/";
        _logger.LogInformation("Fetching chapter images from: {Url}", url);

        var html = await FetchPageAsync(url);

        // Images are injected via: ts_reader.run({..., "sources":[{"source":"...","images":[...]}], ...})
        var match = TsReaderRegex().Match(html);
        if (!match.Success)
        {
            _logger.LogWarning("Could not find ts_reader.run() in page HTML");
            return Array.Empty<string>();
        }

        try
        {
            var json = match.Groups[1].Value;
            using var doc = JsonDocument.Parse(json);

            var sources = doc.RootElement.GetProperty("sources");
            if (sources.GetArrayLength() == 0)
            {
                _logger.LogWarning("No image sources found in ts_reader data");
                return Array.Empty<string>();
            }

            // Use the first source
            var firstSource = sources[0];
            var images = firstSource.GetProperty("images");

            var imageUrls = new List<string>();
            foreach (var img in images.EnumerateArray())
            {
                var imageUrl = img.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    imageUrls.Add(imageUrl);
                }
            }

            _logger.LogInformation("Found {Count} images in chapter", imageUrls.Count);
            return imageUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse ts_reader JSON");
            return Array.Empty<string>();
        }
    }

    public async Task<Stream> DownloadImageAsync(string imageUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.Referrer = new Uri(BaseUrl);
        request.Headers.Add("User-Agent", "MangaMesh-Scraper/1.0");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stream = new MemoryStream();
        await response.Content.CopyToAsync(stream);
        stream.Position = 0;

        return stream;
    }

    private async Task<string> FetchPageAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "MangaMesh-Scraper/1.0");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    private (double? main, double? sub) ParseChapterNumber(string text)
    {
        // Matches "Chapter 1175" or "Chapter 11.1"
        var match = ChapterNumberRegex().Match(text);
        if (!match.Success) return (null, null);

        if (!double.TryParse(match.Groups[1].Value, out var main)) return (null, null);

        if (match.Groups[2].Success && double.TryParse(match.Groups[2].Value, out var sub))
        {
            return (main, sub);
        }

        return (main, null);
    }

    // Matches "Chapter 1175" → groups: (1175, -)
    // Matches "Chapter 11.1" → groups: (11, 1)
    [GeneratedRegex(@"Chapter\s+(\d+)(?:\.(\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterNumberRegex();

    // Extracts the JSON object from ts_reader.run({...})
    [GeneratedRegex(@"ts_reader\.run\((\{.+?\})\);", RegexOptions.Singleline)]
    private static partial Regex TsReaderRegex();
}

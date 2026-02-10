using System.Text.Json;
using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores;

public class JsonSeriesStore : ISeriesStore
{
    private readonly string _filePath = Path.Combine("data", "series", "mock-series.json");
    private readonly Lazy<Task<List<Series>>> _series;

    public JsonSeriesStore()
    {
        _series = new Lazy<Task<List<Series>>>(() => ReadSeriesFromFile());
    }

    private async Task<List<Series>> ReadSeriesFromFile()
    {
        if (!File.Exists(_filePath))
        {
            return new List<Series>();
        }

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<List<Series>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Series>();
    }

    public async Task<IEnumerable<SeriesSearchResult>> GetSeries(string? query, int? limit, int? offset, string? sort = null, string[]? ids = null)
    {
        var allSeries = await _series.Value;

        IEnumerable<Series> seriesQuery = allSeries;

        if (!string.IsNullOrWhiteSpace(query))
        {
            seriesQuery = seriesQuery.Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (ids != null && ids.Any())
        {
            seriesQuery = seriesQuery.Where(s => ids.Contains(s.SeriesId, StringComparer.OrdinalIgnoreCase));
        }

        if (offset.HasValue)
        {
            seriesQuery = seriesQuery.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            seriesQuery = seriesQuery.Take(limit.Value);
        }

        return seriesQuery.Select(s => new SeriesSearchResult(s.Title, s.SeriesId, s.Source, s.ExternalMangaId, 0, DateTime.MinValue));
    }

    public async Task<Series?> GetSeriesDetails(string seriesId)
    {
        var allSeries = await _series.Value;
        return allSeries.FirstOrDefault(s => s.SeriesId.Equals(seriesId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Chapter?> GetChapterDetails(string seriesId, string chapterId)
    {
        var allSeries = await _series.Value;
        var series = allSeries.FirstOrDefault(s => s.SeriesId.Equals(seriesId, StringComparison.OrdinalIgnoreCase));

        return series?.Chapters?.FirstOrDefault(c => c.ChapterId.Contains(chapterId, StringComparison.OrdinalIgnoreCase));
    }
}

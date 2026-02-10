using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Stores;

public interface ISeriesStore
{
    Task<IEnumerable<SeriesSearchResult>> GetSeries(string? query, int? limit, int? offset, string? sort = null, string[]? ids = null);
    Task<Series?> GetSeriesDetails(string seriesId);
    Task<Chapter?> GetChapterDetails(string seriesId, string chapterId);
}


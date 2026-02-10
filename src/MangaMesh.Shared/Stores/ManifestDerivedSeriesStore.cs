using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;

namespace MangaMesh.Shared.Stores
{
    public class ManifestDerivedSeriesStore : ISeriesStore
    {
        private readonly IManifestEntryStore _manifestStore;
        private readonly IMangaMetadataProvider _metadataProvider;
        private readonly ISeriesRegistry _seriesRegistry;

        public ManifestDerivedSeriesStore(IManifestEntryStore manifestStore, IMangaMetadataProvider metadataProvider, ISeriesRegistry seriesRegistry)
        {
            _manifestStore = manifestStore;
            _metadataProvider = metadataProvider;
            _seriesRegistry = seriesRegistry;
        }

        public async Task<IEnumerable<SeriesSearchResult>> GetSeries(string? query, int? limit, int? offset, string? sort = null, string[]? ids = null)
        {
            var entries = await _manifestStore.GetAllAsync();

            // Only consider registered series if possible, or just all series found in manifests?
            // The requirement implies finding "popular" based on manifests.

            var groups = entries.GroupBy(e => e.SeriesId);
            var results = new List<SeriesSearchResult>();

            // We need to fetch metadata for all groups to filter/sort correctly if title is involved,
            // but for popular/recent we might not need title immediately if we can defer.
            // However, we need title for the result.

            foreach (var group in groups)
            {
                var seriesId = group.Key;

                // Optimization: If valid query and seriesId doesn't match and we haven't fetched metadata yet... 
                // But we search by title usually.

                var metadata = await _metadataProvider.GetMangaAsync(seriesId);
                var title = metadata?.CanonicalTitle ?? "Unknown Series"; // Fallback

                // Filter by ID if provided
                if (ids != null && ids.Any() && !ids.Contains(seriesId, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(query) || title.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    var sampleEntry = group.First();
                    Shared.Models.ExternalMetadataSource source = MangaMesh.Shared.Models.ExternalMetadataSource.MangaDex;
                    string externalId = "";

                    if (sampleEntry != null)
                    {
                        Enum.TryParse(sampleEntry.ExternalMetadataSource, out source);
                        externalId = sampleEntry.ExteralMetadataMangaId;
                    }

                    // Calculation
                    var chapterCount = group.Select(e => e.ChapterId).Distinct().Count(); // Unique chapters
                    var lastUploadedAt = group.Max(e => e.AnnouncedUtc);

                    results.Add(new SeriesSearchResult(title, seriesId, source, externalId, chapterCount, lastUploadedAt));
                }
            }

            IEnumerable<SeriesSearchResult> ordered = results;

            if (string.Equals(sort, "popular", StringComparison.OrdinalIgnoreCase))
            {
                // Popular = Most chapters
                ordered = results.OrderByDescending(s => s.ChapterCount);
            }
            else if (string.Equals(sort, "recent", StringComparison.OrdinalIgnoreCase))
            {
                // Recent = Last uploaded
                ordered = results.OrderByDescending(s => s.LastUploadedAt);
            }
            else
            {
                // Default = Title
                ordered = results.OrderBy(s => s.Title);
            }

            var queryable = ordered.AsEnumerable();

            if (offset.HasValue)
                queryable = queryable.Skip(offset.Value);

            if (limit.HasValue)
                queryable = queryable.Take(limit.Value);

            return queryable.ToList();
        }

        public async Task<Series?> GetSeriesDetails(string seriesId)
        {
            var entries = await _manifestStore.GetAllAsync();
            var seriesEntries = entries.Where(e => e.SeriesId.Equals(seriesId, StringComparison.OrdinalIgnoreCase)).ToList();

            var series = await _seriesRegistry.GetByIdAsync(seriesId);

            if (!seriesEntries.Any())
                return null;

            var chapters = seriesEntries
                .GroupBy(e => e.ChapterId)
                .Select(g =>
                {
                    var first = g.First();
                    // Use the most common or first generic chapter number
                    return new Chapter
                    {
                        ChapterId = first.ChapterId,
                        Number = first.ChapterNumber,
                        Volume = first.Volume,
                        Title = first.Title,
                        // Manifests logic normally in GetChapterDetails, logic here is summary
                    };
                })
                .OrderByDescending(c =>
                {
                    return c.Number;
                })
                .ToList();

            return new Series
            {
                SeriesId = seriesId,
                Title = series?.Title ?? "Unknown series",
                //Author = serie
                Chapters = chapters,
                FirstSeenUtc = seriesEntries.Min(e => e.AnnouncedUtc)
            };
        }

        public async Task<Chapter?> GetChapterDetails(string seriesId, string chapterId)
        {
            var entries = await _manifestStore.GetAllAsync();
            var chapterEntries = entries.Where(e =>
                e.SeriesId.Equals(seriesId, StringComparison.OrdinalIgnoreCase) &&
                e.ChapterId.Equals(chapterId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!chapterEntries.Any())
                return null;

            var first = chapterEntries.First();

            return new Chapter
            {
                ChapterId = chapterId,
                Number = first.ChapterNumber,
                Volume = first.Volume,
                Title = first.Title,
                Manifests = chapterEntries.Select(e => new ChapterManifestDto
                {
                    ManifestHash = e.ManifestHash,
                    Language = e.Language,
                    Quality = e.Quality ?? "Unknown",
                    ScanGroup = e.ScanGroup ?? "Unknown",
                    UploadedAt = e.AnnouncedUtc
                }).ToList()
            };
        }
    }
}

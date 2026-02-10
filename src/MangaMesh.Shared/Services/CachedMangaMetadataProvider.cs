using MangaMesh.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace MangaMesh.Shared.Services
{
    public sealed class CachedMangaMetadataProvider : IMangaMetadataProvider
    {
        private readonly IMangaMetadataProvider _inner;
        private readonly MemoryCache _cache;
        private readonly TimeSpan _searchTtl;
        private readonly TimeSpan _metadataTtl;

        public CachedMangaMetadataProvider(
            IMangaMetadataProvider inner,
            TimeSpan searchTtl,
            TimeSpan metadataTtl)
        {
            _inner = inner;
            _searchTtl = searchTtl;
            _metadataTtl = metadataTtl;
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        // ------------------------------
        // Search caching
        // ------------------------------
        public async Task<IReadOnlyList<MangaSearchResult>> SearchMangaAsync(
            string query, int limit = 10)
        {
            var key = $"search:{query.ToLowerInvariant()}:{limit}";

            if (_cache.TryGetValue(key, out IReadOnlyList<MangaSearchResult> cached))
                return cached;

            var results = await _inner.SearchMangaAsync(query, limit);

            _cache.Set(key, results, _searchTtl);
            return results;
        }

        // ------------------------------
        // Manga metadata caching
        // ------------------------------
        public async Task<MangaMetadata?> GetMangaAsync(string externalMangaId)
        {
            var key = $"manga:{externalMangaId}";

            if (_cache.TryGetValue(key, out MangaMetadata cached))
                return cached;

            var result = await _inner.GetMangaAsync(externalMangaId);

            if (result != null)
                _cache.Set(key, result, _metadataTtl);

            return result;
        }

        // ------------------------------
        // Chapter metadata caching
        // ------------------------------
        public async Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(
            string externalMangaId, string language)
        {
            var key = $"chapters:{externalMangaId}:{language}";

            if (_cache.TryGetValue(key, out IReadOnlyList<ChapterMetadata> cached))
                return cached;

            var chapters = await _inner.GetChaptersAsync(externalMangaId, language);

            _cache.Set(key, chapters, _metadataTtl);
            return chapters;
        }


        public async Task<ChapterMetadata?> GetChapterAsync(string externalMangaId, double chapterNumber, string language)
        {
            var key = $"chapter:{externalMangaId}:{chapterNumber}:{language}";

            if (_cache.TryGetValue(key, out ChapterMetadata? cached))
                return cached;

            var result = await _inner.GetChapterAsync(externalMangaId, chapterNumber, language);

            if (result != null)
                _cache.Set(key, result, _metadataTtl);

            return result;
        }
    }
}

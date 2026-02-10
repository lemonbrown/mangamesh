using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Services
{
    public sealed class RateLimitedMangaMetadataProvider : IMangaMetadataProvider
    {
        private readonly IMangaMetadataProvider _inner;
        private readonly SemaphoreSlim _semaphore;
        private readonly TimeSpan _interval;

        public RateLimitedMangaMetadataProvider(IMangaMetadataProvider inner, int maxRequests, TimeSpan perInterval)
        {
            _inner = inner;
            _interval = perInterval;
            _semaphore = new SemaphoreSlim(maxRequests, maxRequests);
        }

        private async Task<T> ExecuteAsync<T>(Func<Task<T>> func)
        {
            await _semaphore.WaitAsync();
            try
            {
                var result = await func();
                return result;
            }
            finally
            {
                // release after interval
                _ = Task.Delay(_interval).ContinueWith(_ => _semaphore.Release());
            }
        }

        public Task<IReadOnlyList<MangaSearchResult>> SearchMangaAsync(string query, int limit = 10)
            => ExecuteAsync(() => _inner.SearchMangaAsync(query, limit));

        public Task<MangaMetadata?> GetMangaAsync(string externalMangaId)
            => ExecuteAsync(() => _inner.GetMangaAsync(externalMangaId));

        public Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(string externalMangaId, string language)
            => ExecuteAsync(() => _inner.GetChaptersAsync(externalMangaId, language));
        public Task<ChapterMetadata?> GetChapterAsync(string externalMangaId, double chapterNumber, string language)
            => ExecuteAsync(() => _inner.GetChapterAsync(externalMangaId, chapterNumber, language));
    }

}

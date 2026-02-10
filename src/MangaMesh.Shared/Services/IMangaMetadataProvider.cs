using MangaMesh.Shared.Models;

namespace MangaMesh.Shared.Services
{
    public interface IMangaMetadataProvider
    {
        // Discovery
        Task<IReadOnlyList<MangaSearchResult>> SearchMangaAsync(
            string query,
            int limit = 10);

        Task<MangaMetadata?> GetMangaAsync(string externalMangaId);
        Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(
            string externalMangaId,
            string language);

        Task<ChapterMetadata?> GetChapterAsync(
            string externalMangaId,
            double chapterNumber,
            string language);
    }
}

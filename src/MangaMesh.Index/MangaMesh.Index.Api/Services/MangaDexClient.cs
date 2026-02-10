using MangaMesh.Index.Api.Mappers;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Models.MangaDex;
using MangaMesh.Shared.Services;

namespace MangaMesh.Index.Api.Services
{
    public sealed class MangaDexClient : IMangaMetadataProvider
    {
        private readonly HttpClient _http;

        public MangaDexClient(HttpClient http)
        {
            _http = http;
            _http.BaseAddress = new Uri("https://api.mangadex.org/");
        }

        public async Task<MangaMetadata?> GetMangaAsync(string mangaId)
        {
            var response = await _http.GetFromJsonAsync<MangaDexMangaResponse>(
                $"manga/{mangaId}");

            if (response?.Data == null)
                return null;

            return MangaDexMapper.MapManga(response.Data);
        }

        public async Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(
            string mangaId,
            string language)
        {
            var url =
                $"chapter?manga={mangaId}&translatedLanguage[]={language}&limit=500";

            var response = await _http
                .GetFromJsonAsync<MangaDexChapterListResponse>(url);

            if (response == null)
                return Array.Empty<ChapterMetadata>();

            return response.Data
                .Select(c => MangaDexMapper.MapChapter(c, mangaId))
                .ToList();
        }

        public async Task<IReadOnlyList<MangaSearchResult>> SearchMangaAsync(
    string query,
    int limit = 10)
        {
            var url =
                $"manga?title={Uri.EscapeDataString(query)}&limit={limit}";

            var response = await _http
                .GetFromJsonAsync<MangaDexSearchResponse>(url);

            if (response == null)
                return Array.Empty<MangaSearchResult>();

            return response.Data
                .Select(MangaDexMapper.MapSearchResult)
                .ToList();
        }

        public async Task<ChapterMetadata?> GetChapterAsync(string mangaId, double chapterNumber, string language)
        {
            var chapterString = chapterNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var url = $"chapter?manga={mangaId}&chapter={chapterString}&translatedLanguage[]={language}&limit=1&includes[]=scanlation_group";

            var response = await _http.GetFromJsonAsync<MangaDexChapterListResponse>(url);

            if (response?.Data == null || response.Data.Count == 0)
                return null;

            return MangaDexMapper.MapChapter(response.Data[0], mangaId);
        }

    }

}

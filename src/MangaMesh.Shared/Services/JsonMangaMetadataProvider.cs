namespace MangaMesh.Shared.Services
{
    using MangaMesh.Shared.Models;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public sealed class JsonMangaMetadataProvider : IMangaMetadataProvider
    {
        private readonly IReadOnlyList<MangaMetadata> _manga;
        private readonly IReadOnlyList<ChapterMetadata> _chapters;

        public JsonMangaMetadataProvider(string path, string filename)
        {
            Directory.CreateDirectory(path);

            if (!File.Exists(Path.Combine(path, filename)))
            {
                File.Create(Path.Combine(path, filename));
            }

            var json = File.ReadAllText(Path.Combine(path, filename));

            if (!string.IsNullOrWhiteSpace(json))
            {

                var data = JsonSerializer.Deserialize<MockMetadataFile>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters =
                        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                        }
                    })
                    ?? throw new InvalidOperationException("Invalid mock metadata file");

                _manga = data.Manga;
                _chapters = data.Chapters;

            }
        }

        public Task<IReadOnlyList<MangaSearchResult>> SearchMangaAsync(
            string query, int limit = 10)
        {
            var results = _manga
                .Where(m =>
                    m.CanonicalTitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    m.AltTitles.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Take(limit)
                .Select(m => new MangaSearchResult
                {
                    Source = m.Source,
                    ExternalMangaId = m.ExternalMangaId,
                    Title = m.CanonicalTitle,
                    AltTitles = m.AltTitles,
                    Status = m.Status
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<MangaSearchResult>>(results);
        }

        public Task<MangaMetadata?> GetMangaAsync(string externalMangaId)
        {
            var manga = _manga.FirstOrDefault(m => m.ExternalMangaId == externalMangaId);
            return Task.FromResult(manga);
        }

        public Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(
            string externalMangaId, string language)
        {
            var chapters = _chapters
                .Where(c =>
                    c.ExternalMangaId == externalMangaId &&
                    c.Language.Equals(language, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.ChapterNumber)
                .ToList();

            return Task.FromResult<IReadOnlyList<ChapterMetadata>>(chapters);
        }
        public Task<ChapterMetadata?> GetChapterAsync(string externalMangaId, double chapterNumber, string language)
        {
            var chapterString = chapterNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var chapter = _chapters.FirstOrDefault(c =>
                c.ExternalMangaId == externalMangaId &&
                string.Equals(c.Language, language, StringComparison.OrdinalIgnoreCase) &&
                (c.ChapterNumber == chapterString || double.TryParse(c.ChapterNumber, out var cn) && Math.Abs(cn - chapterNumber) < 0.001));

            return Task.FromResult(chapter);
        }
    }

}

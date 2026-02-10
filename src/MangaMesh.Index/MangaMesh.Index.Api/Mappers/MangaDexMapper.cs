using MangaMesh.Shared.Models;
using MangaMesh.Shared.Models.MangaDex;

namespace MangaMesh.Index.Api.Mappers
{
    public static class MangaDexMapper
    {
        public static MangaMetadata MapManga(MangaDexMangaData data)
        {
            var attrs = data.Attributes;

            return new MangaMetadata
            {
                Source = ExternalMetadataSource.MangaDex,
                ExternalMangaId = data.Id,
                CanonicalTitle = attrs.Title.Values.First(),
                AltTitles = attrs.AltTitles
                    .SelectMany(t => t.Values)
                    .Distinct()
                    .ToList(),
                Description = attrs.Description?.GetValueOrDefault("en"),
                Status = attrs.Status
            };
        }

        public static ChapterMetadata MapChapter(
            MangaDexChapterData data,
            string mangaId)
        {
            var a = data.Attributes;

            return new ChapterMetadata
            {
                Source = ExternalMetadataSource.MangaDex,
                ExternalChapterId = data.Id,
                ExternalMangaId = mangaId,
                ChapterNumber = a.Chapter,
                Volume = a.Volume,
                Title = a.Title,
                Language = a.TranslatedLanguage,
                PublishDate = a.PublishAt
            };
        }


        public static MangaSearchResult MapSearchResult(
    MangaDexMangaData data)
        {
            var attrs = data.Attributes;

            return new MangaSearchResult
            {
                Source = ExternalMetadataSource.MangaDex,
                ExternalMangaId = data.Id,
                Title = attrs.Title.Values.First(),
                AltTitles = attrs.AltTitles
                    .SelectMany(t => t.Values)
                    .Distinct()
                    .ToList(),
                Status = attrs.Status
            };
        }
    }

}

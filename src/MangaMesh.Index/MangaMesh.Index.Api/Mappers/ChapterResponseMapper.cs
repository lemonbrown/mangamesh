using MangaMesh.Index.Api.Models;
using MangaMesh.Shared.Models;

namespace MangaMesh.Index.Api.Mappers
{
    public static class ChapterResponseMapper
    {
        public static ChapterResponse ToResponse(ChapterMetadata chapter)
        {
            return new ChapterResponse
            {
                ChapterId = chapter.ExternalChapterId,
                ExternalMangaId = chapter.ExternalMangaId,
                Source = chapter.Source,
                ChapterNumber = chapter.ChapterNumber,
                Volume = chapter.Volume,
                Title = chapter.Title,
                Language = chapter.Language,
                PublishDate = chapter.PublishDate
            };
        }

        public static ChapterListResponse ToListResponse(
            string mangaId,
            ExternalMetadataSource source,
            string language,
            IEnumerable<ChapterMetadata> chapters)
        {
            return new ChapterListResponse
            {
                MangaId = mangaId,
                Source = source,
                Language = language,
                Chapters = chapters
                    .OrderBy(c => c.ChapterNumber)
                    .Select(ToResponse)
                    .ToList()
            };
        }
    }

}

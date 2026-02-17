using MangaMesh.SeriesScraper.Models;
using MangaMesh.Shared.Models;
using System.Globalization;

namespace MangaMesh.SeriesScraper.Services;

public interface IMetadataMapper
{
    ChapterMetadata ToChapterMetadata(ChapterInfo chapterInfo, string seriesSlug);
    MangaMetadata ToMangaMetadata(string seriesSlug, string title);
}

public class MetadataMapper : IMetadataMapper
{
    public ChapterMetadata ToChapterMetadata(ChapterInfo chapterInfo, string seriesSlug)
    {
        return new ChapterMetadata
        {
            Source = ExternalMetadataSource.MangaDex,
            ExternalChapterId = chapterInfo.Identifier,
            ExternalMangaId = seriesSlug,
            ChapterNumber = chapterInfo.ChapterNumber.ToString(CultureInfo.InvariantCulture),
            Title = chapterInfo.Title,
            Language = "en",
            PublishDate = chapterInfo.PublishDate
        };
    }

    public MangaMetadata ToMangaMetadata(string seriesSlug, string title)
    {
        return new MangaMetadata
        {
            Source = ExternalMetadataSource.MangaDex,
            ExternalMangaId = seriesSlug,
            CanonicalTitle = title,
            Status = "ongoing"
        };
    }
}

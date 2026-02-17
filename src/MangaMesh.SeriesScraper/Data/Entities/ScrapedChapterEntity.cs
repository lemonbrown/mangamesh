namespace MangaMesh.SeriesScraper.Data.Entities;

public class ScrapedChapterEntity
{
    public int Id { get; set; }
    public required string SiteName { get; set; }
    public required string SeriesSlug { get; set; }
    public required string ChapterIdentifier { get; set; }
    public required double ChapterNumber { get; set; }
    public double? SubChapterNumber { get; set; }
    public string? Title { get; set; }
    public required string OutputPath { get; set; }
    public required string ZipPath { get; set; }
    public int ImageCount { get; set; }
    public DateTime ScrapedAt { get; set; }
    public ScraperStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

namespace MangaMesh.SeriesScraper.Data.Entities;

public class ScraperRunEntity
{
    public int Id { get; set; }
    public required string RunId { get; set; }
    public required string SiteName { get; set; }
    public string? SeriesFilter { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ChaptersProcessed { get; set; }
    public int ChaptersSucceeded { get; set; }
    public int ChaptersFailed { get; set; }
}

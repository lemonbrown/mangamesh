namespace MangaMesh.SeriesScraper.Data.Entities;

public class DownloadedImageEntity
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public required string ImageUrl { get; set; }
    public required string LocalPath { get; set; }
    public int ImageIndex { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DownloadedAt { get; set; }
}

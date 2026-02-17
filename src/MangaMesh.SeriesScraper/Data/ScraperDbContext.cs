using Microsoft.EntityFrameworkCore;
using MangaMesh.SeriesScraper.Data.Entities;

namespace MangaMesh.SeriesScraper.Data;

public class ScraperDbContext : DbContext
{
    public DbSet<ScrapedChapterEntity> ScrapedChapters { get; set; } = default!;
    public DbSet<ScraperRunEntity> ScraperRuns { get; set; } = default!;
    public DbSet<DownloadedImageEntity> DownloadedImages { get; set; } = default!;

    public ScraperDbContext(DbContextOptions<ScraperDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScrapedChapterEntity>()
            .HasIndex(e => new { e.SiteName, e.SeriesSlug, e.ChapterIdentifier })
            .IsUnique();

        modelBuilder.Entity<DownloadedImageEntity>()
            .HasIndex(e => e.ChapterId);

        modelBuilder.Entity<ScraperRunEntity>()
            .HasIndex(e => e.StartedAt);
    }
}

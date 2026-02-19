using MangaMesh.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Data
{
    public class IndexDbContext : DbContext
    {
        public DbSet<IndexKeyEntity> Keys { get; set; } = default!;
        public DbSet<IndexChallengeEntity> Challenges { get; set; } = default!;
        public DbSet<IndexApprovedKeyEntity> ApprovedKeys { get; set; } = default!;
        public DbSet<ManifestEntryEntity> ManifestEntries { get; set; } = default!;
        public DbSet<SeriesDefinitionEntity> SeriesDefinitions { get; set; } = default!;

        public IndexDbContext(DbContextOptions<IndexDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ManifestEntryEntity>(entity =>
            {
                entity.HasIndex(e => e.SeriesId);
                entity.HasIndex(e => e.ChapterId);
            });

            modelBuilder.Entity<SeriesDefinitionEntity>(entity =>
            {
                entity.HasIndex(e => new { e.Source, e.ExternalMangaId });
            });
        }
    }
}

using MangaMesh.Client.Keys;
using MangaMesh.Client.Manifests;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Client.Data
{
    public class ClientDbContext : DbContext
    {
        public DbSet<KeyEntity> Keys { get; set; }
        public DbSet<ManifestEntity> Manifests { get; set; }

        public ClientDbContext(DbContextOptions<ClientDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<KeyEntity>()
                .HasIndex(k => k.PublicKey)
                .IsUnique();

            modelBuilder.Entity<ManifestEntity>()
                .HasIndex(m => m.SeriesId);

            modelBuilder.Entity<ManifestEntity>()
               .HasIndex(m => m.ChapterId);
        }
    }
}

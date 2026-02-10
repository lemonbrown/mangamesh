using MangaMesh.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Data
{
    public class IndexDbContext : DbContext
    {
        public DbSet<IndexKeyEntity> Keys { get; set; } = default!;
        public DbSet<IndexChallengeEntity> Challenges { get; set; } = default!;
        public DbSet<IndexApprovedKeyEntity> ApprovedKeys { get; set; } = default!;

        public IndexDbContext(DbContextOptions<IndexDbContext> options) : base(options)
        {
        }
    }
}

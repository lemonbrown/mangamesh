using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteManifestAnnouncerStore : IManifestAnnouncerStore
    {
        private readonly IndexDbContext _db;

        public SqliteManifestAnnouncerStore(IndexDbContext db)
        {
            _db = db;
        }

        public async Task RecordAsync(string manifestHash, string nodeId, DateTime announcedAt)
        {
            var exists = await _db.ManifestAnnouncers
                .AnyAsync(a => a.ManifestHash == manifestHash && a.NodeId == nodeId);

            if (!exists)
            {
                _db.ManifestAnnouncers.Add(new ManifestAnnouncerEntity
                {
                    ManifestHash = manifestHash,
                    NodeId = nodeId,
                    AnnouncedAt = announcedAt
                });
                await _db.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ManifestAnnouncer>> GetByManifestHashAsync(string manifestHash)
        {
            return await _db.ManifestAnnouncers
                .AsNoTracking()
                .Where(a => a.ManifestHash == manifestHash)
                .Select(a => new ManifestAnnouncer(a.ManifestHash, a.NodeId, a.AnnouncedAt))
                .ToListAsync();
        }

        public async Task DeleteByManifestHashAsync(string manifestHash)
        {
            var entities = await _db.ManifestAnnouncers
                .Where(a => a.ManifestHash == manifestHash)
                .ToListAsync();

            if (entities.Count > 0)
            {
                _db.ManifestAnnouncers.RemoveRange(entities);
                await _db.SaveChangesAsync();
            }
        }
    }
}

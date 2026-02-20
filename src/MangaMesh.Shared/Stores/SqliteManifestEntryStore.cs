using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteManifestEntryStore : IManifestEntryStore
    {
        private readonly IndexDbContext _db;

        public SqliteManifestEntryStore(IndexDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(ManifestEntry entry)
        {
            var existing = await _db.ManifestEntries.FindAsync(entry.ManifestHash);
            if (existing != null)
            {
                existing.LastSeenUtc = entry.LastSeenUtc;
                existing.Title = entry.Title;
            }
            else
            {
                _db.ManifestEntries.Add(new ManifestEntryEntity
                {
                    ManifestHash = entry.ManifestHash,
                    SeriesId = entry.SeriesId,
                    ChapterId = entry.ChapterId,
                    ChapterNumber = entry.ChapterNumber,
                    Volume = entry.Volume,
                    Language = entry.Language,
                    ScanGroup = entry.ScanGroup,
                    Quality = entry.Quality,
                    AnnouncedUtc = entry.AnnouncedUtc,
                    LastSeenUtc = entry.LastSeenUtc,
                    Title = entry.Title,
                    ExternalMetadataSource = entry.ExternalMetadataSource,
                    ExteralMetadataMangaId = entry.ExteralMetadataMangaId
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<ManifestEntry>> GetAllAsync()
        {
            return await _db.ManifestEntries
                .AsNoTracking()
                .Select(e => new ManifestEntry
                {
                    ManifestHash = e.ManifestHash,
                    SeriesId = e.SeriesId,
                    ChapterId = e.ChapterId,
                    ChapterNumber = e.ChapterNumber,
                    Volume = e.Volume,
                    Language = e.Language,
                    ScanGroup = e.ScanGroup,
                    Quality = e.Quality,
                    AnnouncedUtc = e.AnnouncedUtc,
                    LastSeenUtc = e.LastSeenUtc,
                    Title = e.Title,
                    ExternalMetadataSource = e.ExternalMetadataSource,
                    ExteralMetadataMangaId = e.ExteralMetadataMangaId
                })
                .ToListAsync();
        }

        public async Task<ManifestEntry?> GetAsync(string hash)
        {
            var entity = await _db.ManifestEntries
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ManifestHash == hash);

            if (entity == null) return null;

            return new ManifestEntry
            {
                ManifestHash = entity.ManifestHash,
                SeriesId = entity.SeriesId,
                ChapterId = entity.ChapterId,
                ChapterNumber = entity.ChapterNumber,
                Volume = entity.Volume,
                Language = entity.Language,
                ScanGroup = entity.ScanGroup,
                Quality = entity.Quality,
                AnnouncedUtc = entity.AnnouncedUtc,
                LastSeenUtc = entity.LastSeenUtc,
                Title = entity.Title,
                ExternalMetadataSource = entity.ExternalMetadataSource,
                ExteralMetadataMangaId = entity.ExteralMetadataMangaId
            };
        }

        public async Task DeleteAsync(string hash)
        {
            var entity = await _db.ManifestEntries.FindAsync(hash);
            if (entity != null)
            {
                _db.ManifestEntries.Remove(entity);
                await _db.SaveChangesAsync();
            }
        }

        public async Task DeleteBySeriesIdAsync(string seriesId)
        {
            var entities = await _db.ManifestEntries
                .Where(e => e.SeriesId == seriesId)
                .ToListAsync();

            if (entities.Count > 0)
            {
                _db.ManifestEntries.RemoveRange(entities);
                await _db.SaveChangesAsync();
            }
        }
    }
}

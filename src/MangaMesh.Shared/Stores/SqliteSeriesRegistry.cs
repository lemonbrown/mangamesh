using MangaMesh.Shared.Data;
using MangaMesh.Shared.Data.Entities;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MangaMesh.Shared.Stores
{
    public class SqliteSeriesRegistry : ISeriesRegistry
    {
        private readonly IndexDbContext _db;

        public SqliteSeriesRegistry(IndexDbContext db)
        {
            _db = db;
        }

        public async Task<SeriesDefinition?> GetByExternalIdAsync(ExternalMetadataSource source, string externalMangaId)
        {
            var sourceInt = (int)source;
            var entity = await _db.SeriesDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.Source == sourceInt &&
                    e.ExternalMangaId == externalMangaId);

            return entity == null ? null : ToModel(entity);
        }

        public async Task<SeriesDefinition?> GetByIdAsync(string seriesId)
        {
            var entity = await _db.SeriesDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.SeriesId == seriesId);

            return entity == null ? null : ToModel(entity);
        }

        public async Task RegisterAsync(SeriesDefinition definition)
        {
            var existing = await _db.SeriesDefinitions.FindAsync(definition.SeriesId);
            if (existing != null)
            {
                existing.Title = definition.Title;
                existing.Source = (int)definition.Source;
                existing.ExternalMangaId = definition.ExternalMangaId;
            }
            else
            {
                _db.SeriesDefinitions.Add(new SeriesDefinitionEntity
                {
                    SeriesId = definition.SeriesId,
                    Source = (int)definition.Source,
                    ExternalMangaId = definition.ExternalMangaId,
                    Title = definition.Title,
                    CreatedUtc = definition.CreatedUtc
                });
            }

            await _db.SaveChangesAsync();
        }

        public async Task<IEnumerable<SeriesDefinition>> GetAllAsync()
        {
            return await _db.SeriesDefinitions
                .AsNoTracking()
                .Select(e => new SeriesDefinition
                {
                    SeriesId = e.SeriesId,
                    Source = (ExternalMetadataSource)e.Source,
                    ExternalMangaId = e.ExternalMangaId,
                    Title = e.Title,
                    CreatedUtc = e.CreatedUtc
                })
                .ToListAsync();
        }

        private static SeriesDefinition ToModel(SeriesDefinitionEntity entity)
        {
            return new SeriesDefinition
            {
                SeriesId = entity.SeriesId,
                Source = (ExternalMetadataSource)entity.Source,
                ExternalMangaId = entity.ExternalMangaId,
                Title = entity.Title,
                CreatedUtc = entity.CreatedUtc
            };
        }
    }
}

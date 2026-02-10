using MangaMesh.Client.Data;
using MangaMesh.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace MangaMesh.Client.Manifests
{
    public sealed class SqliteManifestStore : IManifestStore
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public SqliteManifestStore(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<bool> ExistsAsync(ManifestHash manifestHash)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            return await context.Manifests.AnyAsync(m => m.Hash == manifestHash.Value);
        }

        public async Task DeleteAsync(ManifestHash hash)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            var entity = await context.Manifests.FindAsync(hash.Value);
            if (entity != null)
            {
                context.Manifests.Remove(entity);
                await context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<ManifestHash>> GetAllHashesAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            return await context.Manifests
                .Select(m => new ManifestHash(m.Hash))
                .ToListAsync();
        }

        public async Task<ChapterManifest?> GetAsync(ManifestHash hash)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            var entity = await context.Manifests.FindAsync(hash.Value);
            if (entity == null) return null;

            return Deserialize(entity.DataJson);
        }

        public async Task<ChapterManifest?> GetBySeriesAndChapterIdAsync(string seriesId, string chapterId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            var entity = await context.Manifests
                .FirstOrDefaultAsync(m => m.SeriesId == seriesId && m.ChapterId == chapterId);

            if (entity == null) return null;

            return Deserialize(entity.DataJson);
        }

        public async Task<(string SetHash, int Count)> GetSetHashAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            var hashes = await context.Manifests
                .OrderBy(m => m.Hash)
                .Select(m => m.Hash)
                .ToListAsync();

            if (hashes.Count == 0)
                return (string.Empty, 0);

            var sb = new StringBuilder();
            foreach (var hash in hashes)
            {
                sb.Append(hash);
            }

            var inputBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hashBytes = SHA256.HashData(inputBytes);

            return (Convert.ToHexString(hashBytes).ToLowerInvariant(), hashes.Count);
        }

        public async Task<ManifestHash> PutAsync(ChapterManifest manifest)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var normalize = manifest with
            {
                Files = manifest.Files.OrderBy(f => f.Path).ToList()
            };
            var json = JsonSerializer.Serialize(normalize, options);
            var bytes = Encoding.UTF8.GetBytes(json);
            var hashBytes = SHA256.HashData(bytes);
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            await SaveAsync(new ManifestHash(hash), manifest);

            return new ManifestHash(hash);
        }

        public async Task SaveAsync(ManifestHash hash, ChapterManifest manifest)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClientDbContext>();
            
            var exists = await context.Manifests.AnyAsync(m => m.Hash == hash.Value);
            if (exists) return;

            var json = JsonSerializer.Serialize(manifest, JsonOptions);

            var entity = new ManifestEntity
            {
                Hash = hash.Value,
                SeriesId = manifest.SeriesId,
                ChapterId = manifest.ChapterId,
                DataJson = json,
                CreatedUtc = DateTime.UtcNow
            };

            context.Manifests.Add(entity);
            await context.SaveChangesAsync();
        }

        private static ChapterManifest? Deserialize(string json)
        {
            return JsonSerializer.Deserialize<ChapterManifest>(json, JsonOptions);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }
}

using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Manifests
{
    public sealed class ManifestStore : IManifestStore
    {
        private readonly string _root;

        public ManifestStore(string rootDirectory)
        {
            _root = rootDirectory;
            Directory.CreateDirectory(_root);
        }

        public Task<IEnumerable<ManifestHash>> GetAllHashesAsync()
        {
            var files = Directory.GetFiles(_root, "*.json", SearchOption.TopDirectoryOnly);

            var hashes = files
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(ManifestHash.Parse)
                .ToList();

            return Task.FromResult<IEnumerable<ManifestHash>>(hashes);
        }

        public async Task SaveAsync(ManifestHash hash, ChapterManifest manifest)
        {
            var path = GetPath(hash);
            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(path, json);
        }

        public Task DeleteAsync(ManifestHash hash)
        {
            var path = GetPath(hash);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(ManifestHash hash)
            => Task.FromResult(File.Exists(GetPath(hash)));

        public async Task<ChapterManifest?> LoadAsync(ManifestHash hash)
        {
            var path = GetPath(hash);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ChapterManifest>(json, JsonOptions);
        }

        private string GetPath(ManifestHash hash)
            => Path.Combine(_root, $"{hash.Value}.json");

        public Task<ManifestHash> PutAsync(ChapterManifest manifest)
        {
            throw new NotImplementedException();
        }

        public async Task<ChapterManifest?> GetAsync(ManifestHash hash)
        {
            var path = GetPath(hash);
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ChapterManifest>(json, JsonOptions);
        }

        public async Task<(string SetHash, int Count)> GetSetHashAsync()
        {
            var hashes = await GetAllHashesAsync();
            var sortedHashes = hashes.OrderBy(h => h.Value).ToList();

            if (sortedHashes.Count == 0)
                return (string.Empty, 0);

            var sb = new StringBuilder();
            foreach (var hash in sortedHashes)
            {
                sb.Append(hash.Value);
            }

            var inputBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hashBytes = SHA256.HashData(inputBytes);

            return (Convert.ToHexString(hashBytes).ToLowerInvariant(), sortedHashes.Count);
        }

        public async Task<ChapterManifest?> GetBySeriesAndChapterIdAsync(string seriesId, string chapterId)
        {
            var hashes = await GetAllHashesAsync();
            foreach (var hash in hashes)
            {
                var manifest = await GetAsync(hash);
                if (manifest != null &&
                    manifest.SeriesId == seriesId &&
                    manifest.ChapterId == chapterId)
                {
                    return manifest;
                }
            }
            return null;
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

}

using System.Collections.Concurrent;
using System.Text.Json;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;

namespace MangaMesh.Shared.Stores
{
    public class JsonManifestEntryStore : IManifestEntryStore
    {
        private readonly string _dataDir;
        private readonly ConcurrentDictionary<string, ManifestEntry> _entries = new();
        private bool _loaded = false;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public JsonManifestEntryStore()
        {
            _dataDir = Path.Combine(Environment.CurrentDirectory, "data", "manifestentries");
            Directory.CreateDirectory(_dataDir);
        }

        public async Task AddAsync(ManifestEntry entry)
        {
            await EnsureLoadedAsync();

            _entries[entry.ManifestHash] = entry;

            var fileName = Path.Combine(_dataDir, $"{entry.ManifestHash}.json");
            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(fileName, json);
        }

        public async Task<IEnumerable<ManifestEntry>> GetAllAsync()
        {
            await EnsureLoadedAsync();
            return _entries.Values;
        }

        public async Task<ManifestEntry?> GetAsync(string hash)
        {
            await EnsureLoadedAsync();
            return _entries.TryGetValue(hash, out var entry) ? entry : null;
        }

        private async Task EnsureLoadedAsync()
        {
            if (_loaded) return;

            await _loadLock.WaitAsync();
            try
            {
                if (_loaded) return;

                if (Directory.Exists(_dataDir))
                {
                    var files = Directory.GetFiles(_dataDir, "*.json");
                    foreach (var file in files)
                    {
                        try
                        {
                            var json = await File.ReadAllTextAsync(file);
                            var entry = JsonSerializer.Deserialize<ManifestEntry>(json);
                            if (entry != null && !string.IsNullOrEmpty(entry.ManifestHash))
                            {
                                _entries[entry.ManifestHash] = entry;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading manifest entry {file}: {ex.Message}");
                        }
                    }
                }

                _loaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }
    }
}

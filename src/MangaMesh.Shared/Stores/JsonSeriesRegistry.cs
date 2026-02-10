using System.Collections.Concurrent;
using System.Text.Json;

using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;

namespace MangaMesh.Shared.Stores
{
    public class JsonSeriesRegistry : ISeriesRegistry
    {
        private readonly string _filePath;
        private readonly ConcurrentDictionary<string, SeriesDefinition> _definitions = new();
        private bool _loaded = false;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public JsonSeriesRegistry()
        {
            var dataDir = Path.Combine(Environment.CurrentDirectory, "data", "series");
            Directory.CreateDirectory(dataDir);
            _filePath = Path.Combine(dataDir, "registry.json");
        }

        private async Task EnsureLoadedAsync()
        {
            if (_loaded) return;

            await _loadLock.WaitAsync();
            try
            {
                if (_loaded) return;

                if (File.Exists(_filePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(_filePath);
                        var list = JsonSerializer.Deserialize<List<SeriesDefinition>>(json);
                        if (list != null)
                        {
                            foreach (var def in list)
                            {
                                _definitions[def.SeriesId] = def;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error loading series registry: {ex.Message}");
                    }
                }
                _loaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task SaveAsync()
        {
            var list = _definitions.Values.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }

        public async Task<SeriesDefinition?> GetByExternalIdAsync(ExternalMetadataSource source, string externalMangaId)
        {
            await EnsureLoadedAsync();
            return _definitions.Values.FirstOrDefault(d =>
                d.Source == source &&
                string.Equals(d.ExternalMangaId, externalMangaId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<SeriesDefinition?> GetByIdAsync(string seriesId)
        {
            await EnsureLoadedAsync();
            _definitions.TryGetValue(seriesId, out var def);
            return def;
        }

        public async Task RegisterAsync(SeriesDefinition definition)
        {
            await EnsureLoadedAsync();
            _definitions[definition.SeriesId] = definition;
            await SaveAsync();
        }

        public async Task<IEnumerable<SeriesDefinition>> GetAllAsync()
        {
            await EnsureLoadedAsync();
            return _definitions.Values;
        }
    }
}

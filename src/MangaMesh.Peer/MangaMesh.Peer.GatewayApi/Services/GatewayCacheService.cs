using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using MangaMesh.Peer.Core.Content;
using Microsoft.Extensions.Options;

namespace MangaMesh.Peer.GatewayApi.Services;

public class GatewayCacheOptions
{
    public int MaxCacheSizeMb { get; set; } = 1024;
    public string CachePath { get; set; } = "data/cache";
}

public class GatewayCacheService : IGatewayCache
{
    private readonly GatewayCacheOptions _options;
    private readonly ILogger<GatewayCacheService> _logger;
    private readonly string _cacheRoot;

    // ConcurrentDictionary for thread-safe access to cache metadata
    // Key: Hash, Value: CacheItemMetadata
    private readonly ConcurrentDictionary<string, CacheItemMetadata> _metadata = new();

    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private long _currentSize = 0;

    private class CacheItemMetadata
    {
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public int AccessCount { get; set; }
        public DateTime LastAccessUtc { get; set; }
        public string Path { get; set; } = string.Empty;

        public double GetScore()
        {
            // Score = (AccessCount * 0.6) + (RecencyWeight * 0.4)
            // RecencyWeight = 1.0 / (TimeSinceLastAccess.TotalMinutes + 1)
            var minutesSinceAccess = (DateTime.UtcNow - LastAccessUtc).TotalMinutes;
            var recencyWeight = 1.0 / (minutesSinceAccess + 1.0);

            // Normalize AccessCount somewhat? 
            // For now, raw count is fine, but maybe cap or log?
            // If AccessCount is 1000, it dominates. 
            // Let's use log scale for access count? Or just raw as requested.
            // Request: (AccessCount * 0.6) + (RecencyWeight * 0.4)
            // Note: RecencyWeight is small (max 1.0). AccessCount is integer >= 1.
            // So AccessCount dominates heavily. This fits "Popular series hot".
            return (AccessCount * 0.6) + (recencyWeight * 0.4);
        }
    }

    public GatewayCacheService(IOptions<GatewayCacheOptions> options, ILogger<GatewayCacheService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _cacheRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.CachePath);

        Directory.CreateDirectory(_cacheRoot);

        // Load index or rebuild from disk
        LoadIndex();
    }

    private void LoadIndex()
    {
        // Simple rebuild from disk for now to ensure consistency
        // In production, we'd save/load a JSON index for faster startup
        var files = Directory.GetFiles(_cacheRoot, "*", SearchOption.AllDirectories);
        long size = 0;

        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var hash = info.Name; // assuming filename is hash

            var item = new CacheItemMetadata
            {
                Hash = hash,
                Size = info.Length,
                AccessCount = 1, // Reset to 1 on restart if no persistent index
                LastAccessUtc = info.LastAccessTimeUtc,
                Path = file
            };

            _metadata[hash] = item;
            size += info.Length;
        }

        _currentSize = size;
        _logger.LogInformation($"[GatewayCache] Initialized. Size: {_currentSize / 1024 / 1024} MB / {_options.MaxCacheSizeMb} MB. Items: {_metadata.Count}");

        // Initial eviction check
        EnsureCapacity(0);
    }

    public async Task<ManifestData?> GetManifestAsync(string hash)
    {
        if (_metadata.TryGetValue(hash, out var item))
        {
            UpdateStats(item);

            try
            {
                if (!File.Exists(item.Path))
                {
                    _metadata.TryRemove(hash, out _);
                    Interlocked.Add(ref _currentSize, -item.Size);
                    return null;
                }

                var json = await File.ReadAllTextAsync(item.Path);
                return JsonSerializer.Deserialize<ManifestData>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to read manifest {hash}: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    public async Task<byte[]?> GetBlobAsync(string hash)
    {
        if (_metadata.TryGetValue(hash, out var item))
        {
            UpdateStats(item);

            try
            {
                if (!File.Exists(item.Path))
                {
                    _metadata.TryRemove(hash, out _);
                    Interlocked.Add(ref _currentSize, -item.Size);
                    return null;
                }

                return await File.ReadAllBytesAsync(item.Path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to read blob {hash}: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    public async Task PutManifestAsync(ManifestData manifest)
    {
        var hash = manifest.ContentHash;
        var json = JsonSerializer.Serialize(manifest);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        await PutBlobInternalAsync(hash, bytes);
    }

    public async Task PutBlobAsync(string hash, byte[] data)
    {
        await PutBlobInternalAsync(hash, data);
    }

    private async Task PutBlobInternalAsync(string hash, byte[] data)
    {
        // Don't cache if larger than max cache size (unlikely but possible)
        if (data.Length > _options.MaxCacheSizeMb * 1024L * 1024L)
        {
            _logger.LogWarning($"Item {hash} too large for cache ({data.Length} bytes). Skipping.");
            return;
        }

        // 1. Evict if needed
        EnsureCapacity(data.Length);

        // 2. Write to disk
        var path = Path.Combine(_cacheRoot, hash);

        // If already exists, just update stats?
        if (_metadata.TryGetValue(hash, out var existing))
        {
            UpdateStats(existing);
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(path, data);

            var item = new CacheItemMetadata
            {
                Hash = hash,
                Size = data.Length,
                AccessCount = 1,
                LastAccessUtc = DateTime.UtcNow,
                Path = path
            };

            _metadata[hash] = item;
            Interlocked.Add(ref _currentSize, data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write to cache {hash}: {ex.Message}");
        }
    }

    private void UpdateStats(CacheItemMetadata item)
    {
        item.AccessCount++;
        item.LastAccessUtc = DateTime.UtcNow;
        // In a real implementation we might want to throttle updates or lock for accuracy, 
        // but for cache stats, eventual consistency is fine.
    }

    private void EnsureCapacity(long incomingSize)
    {
        long maxBytes = _options.MaxCacheSizeMb * 1024L * 1024L;

        // Optimistic check first
        if (_currentSize + incomingSize <= maxBytes) return;

        lock (_ioLock) // Ensure only one eviction cycle runs at a time
        {
            if (_currentSize + incomingSize <= maxBytes) return; // Double check

            _logger.LogInformation($"[GatewayCache] Evicting items. Current: {_currentSize}, Incoming: {incomingSize}, Max: {maxBytes}");

            // Sort by score ascending (lowest score first)
            var sortedItems = _metadata.Values
                .OrderBy(x => x.GetScore())
                .ToList();

            foreach (var item in sortedItems)
            {
                if (_currentSize + incomingSize <= maxBytes) break;

                try
                {
                    if (File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                    }

                    if (_metadata.TryRemove(item.Hash, out _))
                    {
                        Interlocked.Add(ref _currentSize, -item.Size);
                        _logger.LogDebug($"Evicted {item.Hash}. Score: {item.GetScore():F2}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to evict {item.Hash}: {ex.Message}");
                }
            }
        }
    }
}

using MangaMesh.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;

namespace MangaMesh.Peer.GatewayApi.Services;

public class GatewayService
{
    private readonly IDhtNode _dhtNode;
    private readonly IMemoryCache _cache;
    private readonly IBlobStore _blobStore;
    private readonly ILogger<GatewayService> _logger;

    public GatewayService(IDhtNode dhtNode, IMemoryCache cache, IBlobStore blobStore, ILogger<GatewayService> logger)
    {
        _dhtNode = dhtNode;
        _cache = cache;
        _blobStore = blobStore;
        _logger = logger;
    }

    public async Task<ManifestData?> GetManifestAsync(string contentHash)
    {
        // 1. Check Cache
        if (_cache.TryGetValue($"manifest:{contentHash}", out ManifestData? cached))
        {
            return cached;
        }

        var hashBytes = Encoding.UTF8.GetBytes(contentHash);

        // 2. DHT Lookup for providers
        var providers = await _dhtNode.FindValueWithAddressAsync(hashBytes);
        _logger.LogInformation($"[Gateway] Found {providers.Count} providers for hash {contentHash}");
        
        foreach (var provider in providers)
        {
            try
            {
                // 3. Request Manifest
                var request = new GetManifest { ContentHash = contentHash };
                // Timeout of 5 seconds for gateway responsiveness
                var response = await _dhtNode.SendContentRequestAsync(provider.Address, request, TimeSpan.FromSeconds(5));

                if (response is ManifestData data)
                {
                    // 4. Cache and Return
                    _cache.Set($"manifest:{contentHash}", data, TimeSpan.FromMinutes(30)); 
                    return data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to fetch manifest from {provider.Address.Host}:{provider.Address.Port}: {ex.Message}");
            }
        }

        return null;
    }

    public async Task<byte[]?> GetBlobAsync(string hash)
    {
        var blobHash = new BlobHash(hash);

        // 1. Check Local Blob Store
        if (_blobStore.Exists(blobHash))
        {
            using var stream = await _blobStore.OpenReadAsync(blobHash);
            if (stream != null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                return ms.ToArray();
            }
        }

        // 2. DHT Lookup
        var hashBytes = Encoding.UTF8.GetBytes(hash); // Note: DHT might key by Base64 or specific encoding. 
                                                     // DhtNode.StoreAsync uses Key=Hash. 
                                                     // Check if DhtNode FindValue expects same. 
                                                     // InMemoryDhtStorage uses Convert.ToBase64String(hash). 
                                                     // But hash passed here is Hex String. 
                                                     // We need to clarify DHT keying.
                                                     // Current implementation: StoreAsync takes byte[]. 
                                                     // GatewayIntegrationTests used: await _peerNode.StoreAsync(Encoding.UTF8.GetBytes("test-hash"));
                                                     // The hash string itself was treated as bytes of the key.
                                                     // For Content Addressing, the Key IS the blob hash (bytes).
                                                     // So we should Convert Hex to Bytes.
        
        byte[] dhtKey;
        try 
        { 
            dhtKey = Convert.FromHexString(hash); 
        } 
        catch 
        { 
            // Fallback if hash is not hex (e.g. legacy test hash)
            dhtKey = Encoding.UTF8.GetBytes(hash); 
        }

        var providers = await _dhtNode.FindValueWithAddressAsync(dhtKey);
        
        foreach (var provider in providers)
        {
            try
            {
                var request = new GetBlob { BlobHash = hash };
                var response = await _dhtNode.SendContentRequestAsync(provider.Address, request, TimeSpan.FromSeconds(10));
                
                if (response is BlobData data)
                {
                    // 3. Verify Integrity
                    // Compute SHA256 of received data
                    var computedHash = Convert.ToHexString(SHA256.HashData(data.Data)).ToLowerInvariant();
                    if (!string.Equals(computedHash, hash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning($"Hash mismatch for blob {hash}. Got {computedHash}");
                        continue;
                    }

                    // 4. Store locally
                    using var ms = new MemoryStream(data.Data);
                    await _blobStore.PutAsync(ms);

                    return data.Data;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to fetch blob {hash} from {provider.Address}: {ex.Message}");
            }
        }

        return null;
    }

    public async Task<(byte[]? Data, string? MimeType)> GetReassembledFileAsync(string pageHash)
    {
        // 1. Get Page Manifest
        // It's stored as a blob, so we can use GetBlobAsync.
        var manifestBytes = await GetBlobAsync(pageHash);
        if (manifestBytes == null) return (null, null);

        try
        {
            var manifest = JsonSerializer.Deserialize<PageManifest>(manifestBytes);
            if (manifest == null) return (null, null);

            // 2. Allocate buffer
            // Warning: Allocating FileSize in memory. 
            // For large files (e.g. video), this is bad. For images (1-5MB), it's okay.
            if (manifest.FileSize > 100 * 1024 * 1024) // 100MB limit
                throw new InvalidOperationException("File too large for memory reassembly.");

            var fileData = new byte[manifest.FileSize];

            // 3. Fetch chunks
            // Sequential for now to be safe, easy to parallelize later
            // Or use Parallel.ForEachAsync if careful with index
            
            // We need to calculate offsets. Chunks are fixed size except last.
            int offset = 0;
            foreach (var chunkHash in manifest.Chunks)
            {
                var chunkData = await GetBlobAsync(chunkHash);
                if (chunkData == null) 
                    throw new Exception($"Missing chunk {chunkHash} for page {pageHash}");

                if (offset + chunkData.Length > fileData.Length)
                    throw new Exception("Chunk data exceeds declared file size.");

                Array.Copy(chunkData, 0, fileData, offset, chunkData.Length);
                offset += chunkData.Length;
            }

            return (fileData, manifest.MimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to reassemble file {pageHash}");
            return (null, null);
        }
    }
}


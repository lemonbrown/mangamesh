using MangaMesh.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.GatewayApi.Config;

namespace MangaMesh.Peer.GatewayApi.Services;

public class GatewayService
{
    private readonly IDhtNode _dhtNode;
    private readonly IGatewayCache _cache;
    private readonly ILogger<GatewayService> _logger;
    private readonly GatewayConfig _config;

    public GatewayService(IDhtNode dhtNode, IGatewayCache cache, ILogger<GatewayService> logger, GatewayConfig config)
    {
        _dhtNode = dhtNode;
        _cache = cache;
        _logger = logger;
        _config = config;
    }

    public async Task<ManifestData?> GetManifestAsync(string contentHash)
    {
        var result = await GetManifestWithNodesAsync(contentHash);
        return result.Manifest;
    }

    public async Task<(ManifestData? Manifest, List<string> NodeAddresses)> GetManifestWithNodesAsync(string contentHash)
    {
        var nodeAddresses = new List<string>();

        // 1. Check Cache for Manifest
        var cached = await _cache.GetManifestAsync(contentHash);

        byte[] hashBytes;
        try { hashBytes = Convert.FromHexString(contentHash); }
        catch { hashBytes = Encoding.UTF8.GetBytes(contentHash); }

        // 2. DHT Lookup for providers
        var providers = await _dhtNode.FindValueWithAddressAsync(hashBytes);
        _logger.LogInformation($"[Gateway] Found {providers.Count} providers for hash {contentHash}");

        foreach (var p in providers)
        {
            var addr = $"{p.Address.Host}:{p.Address.Port}";
            if (!nodeAddresses.Contains(addr))
            {
                nodeAddresses.Add(addr);
            }
        }

        if (cached != null)
        {
            return (cached, nodeAddresses);
        }

        // 3. If not cached, fetch manifest from one of the providers
        foreach (var provider in providers)
        {
            try
            {
                var request = new GetManifest { ContentHash = contentHash };
                var response = await _dhtNode.SendContentRequestAsync(provider.Address, request, TimeSpan.FromSeconds(5));

                if (response is ManifestData data)
                {
                    await _cache.PutManifestAsync(data);
                    return (data, nodeAddresses);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to fetch manifest from {provider.Address.Host}:{provider.Address.Port}: {ex.Message}");
            }
        }

        return (null, nodeAddresses);
    }

    public async Task<byte[]?> GetBlobAsync(string hash)
    {


        // 1. Check Cache
        var cached = await _cache.GetBlobAsync(hash);
        if (cached != null) return cached;

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

                    // 4. Store in Cache
                    await _cache.PutBlobAsync(hash, data.Data);

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

    public async Task<List<string>> FindPeerUrlsAsync(string contentHash, string apiRelativePath)
    {
        byte[] hashBytes;
        try { hashBytes = Convert.FromHexString(contentHash); }
        catch { hashBytes = Encoding.UTF8.GetBytes(contentHash); }

        var providers = await _dhtNode.FindValueWithAddressAsync(hashBytes);
        _logger.LogInformation($"[Gateway:Redirect] Found {providers.Count} providers for hash {contentHash}");

        return providers
            .Select(p => $"http://{p.Address.Host}:{_config.PeerClientApiPort}/{apiRelativePath}")
            .Distinct()
            .ToList();
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


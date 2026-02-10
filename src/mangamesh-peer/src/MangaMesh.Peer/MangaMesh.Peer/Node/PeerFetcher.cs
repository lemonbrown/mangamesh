using MangaMesh.Client.Blob;
using MangaMesh.Client.Manifests;
using MangaMesh.Client.Tracker;
using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Client.Node
{

    public sealed class PeerFetcher : IPeerFetcher
    {
        private readonly ITrackerClient _trackerClient;
        private readonly IBlobStore _blobStore;
        private readonly IManifestStore _manifestStore;
        private readonly HttpClient _httpClient;

        public PeerFetcher(
            ITrackerClient trackerClient,
            IBlobStore blobStore,
            IManifestStore manifestStore)
        {
            _trackerClient = trackerClient;
            _blobStore = blobStore;
            _manifestStore = manifestStore;
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler);
        }

        public async Task<ManifestHash> FetchManifestAsync(string manifestHash)
        {
            // 1️⃣ Ask tracker for peers
            var peers = await _trackerClient.GetPeersForManifestAsync(manifestHash);
            if (peers.Count == 0) throw new Exception("No peers found for manifest");

            // 2️⃣ Try fetching manifest from peers
            ChapterManifest? manifest = null;
            foreach (var peer in peers)
            {
                try
                {
                    var url = $"https://{peer.IP}:{peer.Port}/api/manifest/{manifestHash}";
                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content.ReadAsStringAsync();
                    manifest = JsonSerializer.Deserialize<ChapterManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (manifest != null) break;
                }
                catch { /* ignore, try next peer */ }
            }

            if (manifest == null) throw new Exception("Failed to fetch manifest from peers");

            // 3️⃣ Download each blob
            var downloadedPages = new List<BlobHash>();
            foreach (var hash in manifest.Files.Select(n => n.Hash))
            {
                var blobHash = new BlobHash(hash);

                if (_blobStore.Exists(blobHash))
                {
                    downloadedPages.Add(blobHash);
                    continue;
                }

                bool downloaded = false;
                foreach (var peer in peers)
                {
                    try
                    {
                        var url = $"https://{peer.IP}:{peer.Port}/api/blob/{blobHash.Value}";
                        var response = await _httpClient.GetAsync(url);
                        if (!response.IsSuccessStatusCode) continue;

                        await using var stream = await response.Content.ReadAsStreamAsync();
                        var storedHash = await _blobStore.PutAsync(stream);

                        // Verify integrity
                        if (storedHash != blobHash)
                            throw new Exception("Blob hash mismatch");

                        downloadedPages.Add(storedHash);
                        downloaded = true;
                        break; // move to next page
                    }
                    catch { /* try next peer */ }
                }

                if (!downloaded)
                    throw new Exception($"Failed to download blob {blobHash.Value}");
            }

            // 4️⃣ Store manifest locally
            await _manifestStore.SaveAsync(new ManifestHash(manifestHash), manifest);

            return new ManifestHash(manifestHash);
        }
    }
}

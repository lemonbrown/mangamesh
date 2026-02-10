using MangaMesh.Peer.Core.Series;
using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Tracker
{
    public sealed class TrackerClient : ITrackerClient
    {
        private readonly HttpClient _httpClient;

        public TrackerClient(HttpClient http)
        {
            _httpClient = http;
        }

        public async Task<bool> PingAsync(string nodeId, string ip, int port, string manifestSetHash, int manifestCount)
        {
            try
            {
                var request = new PingRequest(nodeId, ip, port, manifestSetHash, manifestCount);
                // POST /ping matches the implementation plan and controller expectation
                var response = await _httpClient.PostAsJsonAsync("/ping", request);

                // 200 OK = Synced
                // 409 Conflict = Sync Needed
                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch
            {
                // Fallback to sync needed on error
                return false;
            }
        }

        /// <summary>
        /// Query tracker for peers hosting a manifest
        /// </summary>
        public async Task<List<PeerInfo>> GetPeersForManifestAsync(string manifestHash)
        {
            var response = await _httpClient.GetAsync($"/manifest/{manifestHash}/peers");
            response.EnsureSuccessStatusCode();

            var peers = await response.Content.ReadFromJsonAsync<List<PeerInfo>>();
            return peers ?? new List<PeerInfo>();
        }

        public async Task<PeerInfo?> GetPeerAsync(string seriesId, string chapterId, string manifestHash)
        {
            try
            {
                var query = $"?seriesId={Uri.EscapeDataString(seriesId)}&chapterId={Uri.EscapeDataString(chapterId)}&manifestHash={Uri.EscapeDataString(manifestHash)}";
                var response = await _httpClient.GetAsync($"/peer{query}");

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<PeerInfo>();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Announce this node and the manifests it hosts
        /// </summary>
        public async Task AnnounceAsync(Shared.Models.AnnounceRequest announcementRequest)
        {
            var request = new AnnounceRequest(announcementRequest.NodeId, announcementRequest.IP, announcementRequest.Port, announcementRequest.Manifests);
            var response = await _httpClient.PostAsJsonAsync("/announce", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task AnnounceManifestAsync(
             Shared.Models.AnnounceManifestRequest announcement,
             CancellationToken ct = default)
        {
            var nodeId = string.IsNullOrEmpty(announcement.NodeId)
                ? Guid.NewGuid().ToString("N")
                : announcement.NodeId;

            dynamic content = new
            {
                NodeId = nodeId,
                ManifestHash = announcement.ManifestHash,
                announcement.SeriesId,
                announcement.ChapterId,
                announcement.ChapterNumber,
                announcement.Volume,
                announcement.Source,
                announcement.ExternalMangaId,
                announcement.Title,
                announcement.Language,
                announcement.ScanGroup,
                announcement.TotalSize,
                announcement.CreatedUtc,
                announcement.AnnouncedAt,
                announcement.Signature,
                announcement.PublicKey,
                announcement.SignedBy,
                announcement.Files
            };

            var httpContent = JsonContent.Create(content);

            var response = await _httpClient.PostAsync("/api/announce/manifest", httpContent);

            if (response.IsSuccessStatusCode)
                return;

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                throw new InvalidOperationException("Manifest already exists on the tracker.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exceptions.TrackerAuthenticationException("Tracker session expired or invalid.");
            }

            var error = await response.Content.ReadAsStringAsync(ct);

            throw new InvalidOperationException(
                $"Tracker announce failed ({response.StatusCode}): {error}");
        }
        public async Task<bool> CheckNodeExistsAsync(string nodeId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, $"/nodes/{nodeId}");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(string SeriesId, string Title)> RegisterSeriesAsync(Shared.Models.ExternalMetadataSource source, string externalMangaId)
        {
            var request = new
            {
                Source = source,
                ExternalMangaId = externalMangaId
            };

            var response = await _httpClient.PostAsJsonAsync("/api/series/register", request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RegisterSeriesResponse>();
            if (result == null) throw new InvalidOperationException("Failed to register series: Empty response");

            return (result.SeriesId, result.Title);
        }

        private class RegisterSeriesResponse
        {
            public string SeriesId { get; set; } = "";
            public string Title { get; set; } = "";
        }
        public async Task<IEnumerable<SeriesSummaryResponse>> SearchSeriesAsync(string query, string? sort = null, string[]? ids = null)
        {
            var url = $"/api/series?q={Uri.EscapeDataString(query ?? "")}";
            if (!string.IsNullOrEmpty(sort))
            {
                url += $"&sort={Uri.EscapeDataString(sort)}";
            }
            if (ids != null && ids.Any())
            {
                foreach (var id in ids)
                {
                    url += $"&ids={Uri.EscapeDataString(id)}";
                }
            }
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IEnumerable<SeriesSummaryResponse>>()
                   ?? Enumerable.Empty<SeriesSummaryResponse>();
        }

        public async Task<TrackerStats> GetStatsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/stats");
                if (!response.IsSuccessStatusCode) return new TrackerStats();

                return await response.Content.ReadFromJsonAsync<TrackerStats>() ?? new TrackerStats();
            }
            catch
            {
                return new TrackerStats();
            }
        }
        public async Task<KeyChallengeResponse> CreateChallengeAsync(string publicKeyBase64)
        {
            var encodedKey = Uri.EscapeDataString(publicKeyBase64);
            var response = await _httpClient.PostAsync($"/api/keys/{encodedKey}/challenges", null);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<KeyChallengeResponse>()
                ?? throw new InvalidOperationException("Failed to create challenge: Empty response");
        }

        public async Task<KeyVerificationResponse> VerifyChallengeAsync(string publicKeyBase64, string challengeId, string signatureBase64)
        {
            var encodedKey = Uri.EscapeDataString(publicKeyBase64);
            var request = new KeyVerificationRequest
            {
                ChallengeId = challengeId,
                SignatureBase64 = signatureBase64
            };

            var response = await _httpClient.PostAsJsonAsync($"/api/keys/{encodedKey}/challenges/{challengeId}/verify", request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<KeyVerificationResponse>()
                ?? throw new InvalidOperationException("Failed to verify challenge: Empty response");
        }

        public async Task AuthorizeManifestAsync(Shared.Models.AuthorizeManifestRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/announce/authorize", request);
            response.EnsureSuccessStatusCode();
        }

    }
}

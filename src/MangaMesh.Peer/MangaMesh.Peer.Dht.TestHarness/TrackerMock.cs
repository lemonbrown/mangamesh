
    public class TrackerMock : MangaMesh.Peer.Core.Tracker.ITrackerClient
    {
        public Task AnnounceAsync(MangaMesh.Shared.Models.AnnounceRequest announceRequest)
        {
            return Task.CompletedTask;
        }

        public Task AnnounceManifestAsync(MangaMesh.Shared.Models.AnnounceManifestRequest announcement, CancellationToken ct = default)
        {
             return Task.CompletedTask;
        }

        public Task AuthorizeManifestAsync(MangaMesh.Shared.Models.AuthorizeManifestRequest request)
        {
             return Task.CompletedTask;
        }

        public Task<bool> CheckNodeExistsAsync(string nodeId)
        {
             return Task.FromResult(true);
        }

        public Task<MangaMesh.Shared.Models.KeyChallengeResponse> CreateChallengeAsync(string publicKeyBase64)
        {
             throw new NotImplementedException();
        }

        public Task<MangaMesh.Shared.Models.PeerInfo?> GetPeerAsync(string seriesId, string chapterId, string manifestHash)
        {
             return Task.FromResult<MangaMesh.Shared.Models.PeerInfo?>(null);
        }

        public Task<List<MangaMesh.Shared.Models.PeerInfo>> GetPeersForManifestAsync(string manifestHash)
        {
             return Task.FromResult(new List<MangaMesh.Shared.Models.PeerInfo>());
        }

        public Task<MangaMesh.Peer.Core.Tracker.TrackerStats> GetStatsAsync()
        {
             return Task.FromResult(new MangaMesh.Peer.Core.Tracker.TrackerStats());
        }

        public Task<bool> PingAsync(string nodeId, string ip, int port, string manifestSetHash, int manifestCount)
        {
             return Task.FromResult(true);
        }

        public Task<(string SeriesId, string Title)> RegisterSeriesAsync(MangaMesh.Shared.Models.ExternalMetadataSource source, string externalMangaId)
        {
             throw new NotImplementedException();
        }

        public Task<IEnumerable<MangaMesh.Shared.Models.SeriesSummaryResponse>> SearchSeriesAsync(string query, string? sort = null, string[]? ids = null)
        {
             return Task.FromResult(Enumerable.Empty<MangaMesh.Shared.Models.SeriesSummaryResponse>());
        }

        public Task<MangaMesh.Shared.Models.KeyVerificationResponse> VerifyChallengeAsync(string publicKeyBase64, string challengeId, string signatureBase64)
        {
             throw new NotImplementedException();
        }
    }

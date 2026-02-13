using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Metadata;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Subscriptions;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace MangaMesh.Peer.Core.Replication
{
    public sealed class ReplicationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReplicationService> _logger;
        private readonly INodeConnectionInfoProvider _connectionInfo;

        private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
        private readonly INodeIdentityService _nodeIdentity;

        private readonly ConcurrentDictionary<string, PeerInfo> _connectedPeers
            = new();

        public string NodeId => _nodeIdentity.NodeId;

        public ReplicationService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReplicationService> logger,
            INodeIdentityService nodeIdentity,
            INodeConnectionInfoProvider connectionInfo)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _nodeIdentity = nodeIdentity;
            _connectionInfo = connectionInfo;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReplicationService started");


            // Initial Identity Verification (Startup) - Removed as we don't use Auth Sessions anymore
            // await _nodeIdentity.VerifyIdentityAsync();


            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var tracker = scope.ServiceProvider.GetRequiredService<ITrackerClient>();
                    var fetcher = scope.ServiceProvider.GetRequiredService<IPeerFetcher>();
                    var subscriptionStore = scope.ServiceProvider.GetRequiredService<ISubscriptionStore>();
                    var manifests = scope.ServiceProvider.GetRequiredService<IManifestStore>();
                    var metadata = scope.ServiceProvider.GetRequiredService<IMetadataClient>();

                    await AnnounceAsync(tracker, manifests);
                    //await SyncSubscriptionsAsync(
                    //    subscriptionStore,
                    //    manifests,
                    //    metadata,
                    //    fetcher
                    //);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Replication loop error");
                    _nodeIdentity.UpdateStatus(false);
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        // 🔊 Announce manifests we host
        private async Task AnnounceAsync(
            ITrackerClient tracker,
            IManifestStore manifests)
        {
            try
            {
            // var (ip, port) = await _connectionInfo.GetConnectionInfoAsync(); // No longer needed

                // 1. Calculate smart sync hash
                var (setHash, count) = await manifests.GetSetHashAsync();

                // 2. Try lightweight Ping
                var isSynced = await tracker.PingAsync(
                    _nodeIdentity.NodeId,
                    setHash,
                    count
                );

                if (isSynced)
                {
                    _logger.LogInformation("Tracker synced (Smart Ping)");
                    _nodeIdentity.UpdateStatus(true);
                    return;
                }

                // 3. Fallback to full Announce
                _logger.LogInformation("Tracker mismatch, performing full announce...");
                var manifestHashes = await manifests.GetAllHashesAsync();

                var announcementRequest = new Shared.Models.AnnounceRequest
                (
                    _nodeIdentity.NodeId,
                    manifestHashes.Select(m => m.Value).ToList());

                //await tracker.AnnounceAsync(
                //    announcementRequest
                //);

                _logger.LogInformation("Announced {count} manifests", manifestHashes.Count());
                _nodeIdentity.UpdateStatus(true);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Unable to announce");
                _nodeIdentity.UpdateStatus(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during announce");
                _nodeIdentity.UpdateStatus(false);
            }
        }

        // 📚 Sync subscribed series
        private async Task SyncSubscriptionsAsync(
            ISubscriptionStore subscriptionStore,
            IManifestStore manifests,
            IMetadataClient metadata,
            IPeerFetcher fetcher)
        {
            var subs = await subscriptionStore.GetAllAsync();

            foreach (var sub in subs.Where(s => s.AutoFetch))
            {
                await SyncSeriesAsync(
                    sub.SeriesId,
                    sub.Language,
                    manifests,
                    metadata,
                    fetcher
                );
            }
        }

        // 📖 Sync one series
        private async Task SyncSeriesAsync(
            string seriesId,
            string language,
            IManifestStore manifests,
            IMetadataClient metadata,
            IPeerFetcher fetcher)
        {
            var chapters = await metadata.GetChaptersAsync(seriesId, language);

            foreach (var chapter in chapters)
            {
                var manifestHash = new ManifestHash(chapter.ManifestHash);

                if (await manifests.ExistsAsync(manifestHash))
                    continue;

                _logger.LogInformation(
                    "Fetching {series} Chapter {chapter}",
                    seriesId,
                    chapter.ChapterNumber
                );

                try
                {
                    await fetcher.FetchManifestAsync(chapter.ManifestHash);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch chapter");
                }
            }
        }

        public int GetConnectedPeersCount()
        {
            var now = DateTime.UtcNow;
            return _connectedPeers.Values.Count(p => (now - p.LastSeen).TotalSeconds < 120);
        }

    }
}

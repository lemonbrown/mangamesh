using MangaMesh.Peer.Core.Keys;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.Core.Tracker
{
    /// <summary>
    /// Handles the challenge-response authentication sub-protocol and then publishes
    /// a manifest to the tracker. Extracted from ImportChapterService so the flow
    /// can be reused wherever manifests are published.
    /// </summary>
    public sealed class TrackerPublisher : ITrackerPublisher
    {
        private readonly ITrackerChallengeClient _challengeClient;
        private readonly IManifestAnnouncer _manifestAnnouncer;
        private readonly IKeyStore _keyStore;
        private readonly IKeyPairService _keyPairService;
        private readonly ILogger<TrackerPublisher> _logger;

        public TrackerPublisher(
            ITrackerChallengeClient challengeClient,
            IManifestAnnouncer manifestAnnouncer,
            IKeyStore keyStore,
            IKeyPairService keyPairService,
            ILogger<TrackerPublisher> logger)
        {
            _challengeClient = challengeClient;
            _manifestAnnouncer = manifestAnnouncer;
            _keyStore = keyStore;
            _keyPairService = keyPairService;
            _logger = logger;
        }

        public async Task PublishManifestAsync(Shared.Models.AnnounceManifestRequest request, CancellationToken ct = default)
        {
            // 1. Get identity keys
            var keys = await _keyStore.GetAsync();
            if (keys == null)
                throw new InvalidOperationException("Cannot publish manifest: No identity keys found.");

            // 2. Request challenge from tracker
            var challenge = await _challengeClient.CreateChallengeAsync(keys.PublicKeyBase64);

            // 3. Solve challenge
            var signature = _keyPairService.SolveChallenge(challenge.Nonce, keys.PrivateKeyBase64);

            // 4. Authorize manifest
            var authRequest = new Shared.Models.AuthorizeManifestRequest
            {
                ChallengeId = challenge.ChallengeId,
                SignatureBase64 = signature,
                ManifestHash = request.ManifestHash.Value,
                NodeId = request.NodeId,
                PublicKeyBase64 = keys.PublicKeyBase64
            };

            await _manifestAnnouncer.AuthorizeManifestAsync(authRequest);

            // 5. Announce manifest (tracker validates authorization)
            await _manifestAnnouncer.AnnounceManifestAsync(request, ct);

            _logger.LogDebug("Manifest {Hash} published successfully", request.ManifestHash.Value);
        }
    }
}

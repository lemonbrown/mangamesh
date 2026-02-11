using MangaMesh.Shared.Services;
using MangaMesh.Index.Api.Services;
using System.Security.Cryptography;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Stores;
using Microsoft.AspNetCore.Mvc;

namespace MangaMesh.Index.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TrackerController : ControllerBase
    {
        private readonly ILogger<TrackerController> _logger;
        private readonly INodeRegistry _nodeRegistry;
        private readonly ICoverService _coverService;
        private readonly IManifestEntryStore _manifestEntryStore;
        private readonly IMangaMetadataProvider _metadataProvider;
        private readonly ISeriesRegistry _seriesRegistry;
        private readonly IApprovedKeyStore _approvedKeyStore;
        private readonly MangaMesh.Shared.Services.IManifestAuthorizationService _authService;
        private readonly IPublicKeyRegistry _keyRegistry; // Need this for manual verification if not injected yet. Check constructor. 
        // Actually PublicKeyRegistry isn't injected yet. Let's check constructor params.

        public TrackerController(
            ILogger<TrackerController> logger,
            INodeRegistry nodeRegistry,
            IManifestEntryStore manifestEntryStore,
            IMangaMetadataProvider metadataProvider,
            ISeriesRegistry seriesRegistry,
            IApprovedKeyStore approvedKeyStore,


            MangaMesh.Shared.Services.IManifestAuthorizationService authService,
            IPublicKeyRegistry keyRegistry,
            ICoverService coverService)
        {
            _logger = logger;
            _nodeRegistry = nodeRegistry;
            _manifestEntryStore = manifestEntryStore;
            _metadataProvider = metadataProvider;
            _seriesRegistry = seriesRegistry;
            _approvedKeyStore = approvedKeyStore;
            _authService = authService;
            _keyRegistry = keyRegistry;
            _coverService = coverService;
        }

        [HttpGet("/api/keys/{publicKeyBase64}/allowed")]
        public async Task<IResult> IsKeyAllowed(string publicKeyBase64)
        {
            var decoded = Uri.UnescapeDataString(publicKeyBase64);
            var isAllowed = await _approvedKeyStore.IsKeyApprovedAsync(decoded);
            return Results.Json(new { Allowed = isAllowed });
        }

        // [HttpPost("/api/keys/{publicKeyBase64}/approve")] -- internal/admin only, omitting for now or implementing if needed for testing

        [HttpPost("/announce")]
        public async Task<IResult> Announce(AnnounceRequest request)
        {
            // ... (existing Announce logic untouched) ...
            // Calculate set hash for smart sync
            var sortedHashes = request.Manifests.OrderBy(h => h).ToList();
            var manifestSetHash = "";
            // ...
            if (sortedHashes.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var hash in sortedHashes)
                {
                    sb.Append(hash);
                }
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
                var hashBytes = SHA256.HashData(inputBytes);
                manifestSetHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            var node = new TrackerNode()
            {
                NodeId = request.NodeId,
                Manifests = new HashSet<string>(request.Manifests),
                ManifestSetHash = manifestSetHash,
                ManifestCount = request.Manifests.Count
            };

            // Populate ManifestDetails for Seed Counting
            foreach (var hash in request.Manifests)
            {
                var entry = await _manifestEntryStore.GetAsync(hash);
                if (entry != null && double.TryParse(entry.ChapterNumber.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var chNum))
                {
                    node.ManifestDetails[hash] = (entry.SeriesId, chNum);
                }
            }

            if (node == null) return Results.BadRequest();

            _nodeRegistry.RegisterOrUpdate(node);

            return Results.Ok();
        }

        [HttpPost("/ping")]
        public IResult Ping(PingRequest request)
        {
            // ... (existing Ping logic) ...
            var node = _nodeRegistry.GetNode(request.NodeId);

            // If node not found, we need a sync/announce
            if (node == null)
            {
                return Results.Conflict();
            }

            // Update simple stats
            node.LastSeen = DateTime.UtcNow;

            // Check if manifest set matches
            if (node.ManifestSetHash != request.ManifestSetHash ||
                node.ManifestCount != request.ManifestCount)
            {
                // Mismatch -> Client must Announce full list
                return Results.Conflict();
            }

            return Results.Ok();
        }

        [HttpGet("/manifest/{hash}/peers")]
        public async Task<IResult> GetPeersByManifest(string hash)
        {
            // ... (existing logic) ...
            var peers = _nodeRegistry.GetPeersForManifest(hash)
               .Select(n => new { n.NodeId })
               .ToList();

            return Results.Json(peers);
        }

        [HttpGet("/peer")]
        public IResult GetPeer(string seriesId, string chapterId, string manifestHash)
        {
            // ... (existing logic) ...
            var peers = _nodeRegistry.GetPeersForManifest(manifestHash);

            if (peers.Count == 0)
            {
                return Results.NotFound();
            }

            var peer = peers[Random.Shared.Next(peers.Count)];

            return Results.Json(new { peer.NodeId });
        }



        [HttpPost("/api/announce/authorize")]
        public async Task<IResult> AuthorizeManifest([FromBody] AuthorizeManifestRequest request)
        {
            // Verify the challenge signature
            // We use VerifyChallengeAsync from registry. 
            // Note: Currently VerifyChallengeAsync expects matching ChallengeId and Signature. 
            // It relies on the ChallengeStore.

            // Note: VerifyChallengeAsync in PublicKeyRegistry deletes the challenge if valid.
            // That's exactly what we want: one-time use of the challenge to authorize the manifest.

            var result = await _keyRegistry.VerifyChallengeAsync(request.ChallengeId, Convert.FromBase64String(request.SignatureBase64));

            if (!result.Valid)
            {
                return Results.BadRequest("Invalid challenge signature or expired challenge.");
            }

            // Authorization successful -> Add to in-memory store
            _authService.Authorize(request.NodeId, request.ManifestHash);

            return Results.Ok();
        }

        [HttpPost("/api/announce/manifest")]
        public async Task<IResult> AnnounceManifest([FromBody] AnnounceManifestRequest request)
        {
            // 0. Authorization Check (Pre-Authorization via Challenge)
            if (!_authService.Consume(request.NodeId, request.ManifestHash.Value))
            {
                return Results.Json(new { message = "Manifest announcement not authorized. Please perform challenge-response authorization first." }, statusCode: 401);
            }

            if (string.IsNullOrWhiteSpace(request.Signature))
            {
                return Results.Unauthorized();
            }

            var node = _nodeRegistry.GetNode(request.NodeId);
            if (node == null)
            {
                return Results.NotFound();
            }

            var effectiveTitle = request.Title;

            // Verify Metadata & Series ID (existing logic)
            if (!string.IsNullOrEmpty(request.ExternalMangaId))
            {
                // ...
                var seriesDef = await _seriesRegistry.GetByIdAsync(request.SeriesId);
                if (seriesDef == null)
                {
                    return Results.BadRequest($"Unknown SeriesId: {request.SeriesId}. Please register the series first.");
                }

                if (seriesDef.Source != request.Source ||
                    !string.Equals(seriesDef.ExternalMangaId, request.ExternalMangaId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Manifest mismatch...");
                    return Results.BadRequest("SeriesId does not match the provided External Metadata.");
                }

                // ...
                if (!seriesDef.Title.Contains(request.Title, StringComparison.OrdinalIgnoreCase) &&
                   !request.Title.Contains(seriesDef.Title, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Manifest announcement rejected: Title mismatch...");
                    return Results.BadRequest($"Title mismatch. Expected something similar to '{seriesDef.Title}'");
                }

                if (double.TryParse(request.ChapterNumber.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var chNum))
                {
                    var authoritative = await _metadataProvider.GetChapterAsync(seriesDef.ExternalMangaId, chNum, request.Language);
                    if (authoritative != null && !string.IsNullOrWhiteSpace(authoritative.Title))
                    {
                        effectiveTitle = authoritative.Title;
                    }
                }
            }

            // Check for duplicate manifest
            if (await _manifestEntryStore.GetAsync(request.ManifestHash.Value) != null)
            {
                // Actually, if it's a conflict, maybe we don't consume it? 
                // "Once processed... no longer usable." 
                // Conflict means it was already processed.
                // Let's consume it to be safe.
                return Results.Conflict(new { message = "Manifest already exists." });
            }

            // 1. Reconstruct what should have been signed
            var manifestToVerify = new Shared.Models.ChapterManifest
            {
                SchemaVersion = request.SchemaVersion,
                ChapterNumber = request.ChapterNumber,
                ChapterId = request.ChapterId,
                CreatedUtc = request.CreatedUtc,
                Language = request.Language,
                ScanGroup = request.ScanGroup,
                SeriesId = request.SeriesId,
                Title = request.Title,
                Volume = request.Volume,
                TotalSize = request.TotalSize,
                SignedBy = request.SignedBy,
                PublicKey = request.PublicKey,
                Files = request.Files
            };

            // Calculate the metadata hash
            byte[] manifestBytes = ManifestSigningService.SerializeCanonical(manifestToVerify);
            byte[] hash = SHA256.HashData(manifestBytes);
            string metadataHash = $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";

            var signedManifest = new SignedChapterManifest
            {
                Manifest = manifestToVerify,
                ManifestHash = metadataHash,
                PublisherPublicKey = request.PublicKey,
                Signature = request.Signature
            };

            // 2. Verify signature
            try
            {
                ManifestSigningService.VerifySignedManifest(signedManifest);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Invalid manifest signature from node {NodeId}", request.NodeId);
                return Results.BadRequest("Invalid signature");
            }

            // 4. Save to store
            var entry = new ManifestEntry
            {
                ManifestHash = request.ManifestHash.ToString(),
                SeriesId = request.SeriesId,
                ChapterId = request.ChapterId,
                ChapterNumber = request.ChapterNumber,
                Volume = request.Volume,
                Language = request.Language,
                ScanGroup = request.ScanGroup,
                AnnouncedUtc = request.AnnouncedAt.UtcDateTime,
                LastSeenUtc = DateTime.UtcNow,
                Title = effectiveTitle,
                ExternalMetadataSource = request.Source.ToString(),
                ExteralMetadataMangaId = request.ExternalMangaId
            };

            await _manifestEntryStore.AddAsync(entry);
            _nodeRegistry.AddManifestToNode(request.NodeId, request.ManifestHash.Value);

            _nodeRegistry.AddManifestToNode(request.NodeId, request.ManifestHash.Value);

            // 5. Ensure cover is cached (Fire-and-forget or awaited? Awaited for now to ensure consistency, but logging inside handles errors)
            // We pass ExternalMangaId if available, or we might need to look it up from series registry if not passed?
            // Request has ExternalMangaId.
            if (!string.IsNullOrEmpty(request.ExternalMangaId))
            {
                await _coverService.EnsureCoverCachedAsync(request.ExternalMangaId);
            }

            return Results.Ok();
        }

        [HttpHead("/nodes/{nodeId}")]
        public IResult CheckNodeExists(string nodeId)
        {
            var node = _nodeRegistry.GetNode(nodeId);
            return node != null ? Results.Ok() : Results.NotFound();
        }

        [HttpGet("stats")]
        public async Task<IResult> GetStats()
        {
            var nodeCount = _nodeRegistry.GetNodeCount();
            // ManifestEntryStore doesn't have count yet? 
            // We can just skip manifest count for now or add it later if needed.
            // Dashboard only asked for Peer Count.

            return Results.Ok(new
            {
                NodeCount = nodeCount
            });
        }

        [HttpGet("/tracker/nodes")]
        public IResult GetNodes()
        {
            // Simple endpoint for Admin API to consume
            return Results.Json(_nodeRegistry.GetAllNodes());
        }
    }
}

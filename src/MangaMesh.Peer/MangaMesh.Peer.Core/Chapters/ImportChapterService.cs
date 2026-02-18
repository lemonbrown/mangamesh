using MangaMesh.Shared.Helpers;
using MangaMesh.Shared.Services;
using NSec.Cryptography;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MangaMesh.Peer.Core.Chapters
{
    public class ImportChapterService : IImportChapterService
    {
        private readonly IBlobStore _blobStore;
        private readonly IManifestStore _manifestStore;
        private readonly ISeriesRegistry _seriesRegistry;
        private readonly IKeyStore _keyStore;
        private readonly INodeIdentity _nodeIdentity;
        private readonly IChunkIngester _chunkIngester;
        private readonly ITrackerPublisher _trackerPublisher;
        private readonly IEnumerable<IChapterSourceReader> _sourceReaders;
        private readonly IImageFormatProvider _imageFormats;
        private readonly IManifestSigningService _manifestSigning;
        private readonly IDhtNode _dhtNode;
        private readonly ILogger<ImportChapterService> _logger;

        public ImportChapterService(
            IBlobStore blobStore,
            IManifestStore manifestStore,
            ISeriesRegistry seriesRegistry,
            IKeyStore keyStore,
            INodeIdentity nodeIdentity,
            IChunkIngester chunkIngester,
            ITrackerPublisher trackerPublisher,
            IEnumerable<IChapterSourceReader> sourceReaders,
            IImageFormatProvider imageFormats,
            IManifestSigningService manifestSigning,
            IDhtNode dhtNode,
            ILogger<ImportChapterService> logger)
        {
            _blobStore = blobStore;
            _manifestStore = manifestStore;
            _seriesRegistry = seriesRegistry;
            _keyStore = keyStore;
            _nodeIdentity = nodeIdentity;
            _chunkIngester = chunkIngester;
            _trackerPublisher = trackerPublisher;
            _sourceReaders = sourceReaders;
            _imageFormats = imageFormats;
            _manifestSigning = manifestSigning;
            _dhtNode = dhtNode;
            _logger = logger;
        }

        public async Task<ImportChapterResult> ImportAsync(ImportChapterRequest request, CancellationToken ct = default)
        {
            var reader = _sourceReaders.FirstOrDefault(r => r.CanRead(request.SourceDirectory))
                ?? throw new DirectoryNotFoundException($"Source path not found or unsupported: {request.SourceDirectory}");

            List<Shared.Models.ChapterFileEntry> entries = new();
            var pageManifestHashes = new List<BlobHash>();
            long totalSize = 0;

            await foreach (var (name, content) in reader.ReadFilesAsync(request.SourceDirectory, ct))
            {
                using (content)
                {
                    var mimeType = _imageFormats.GetMimeType(name);
                    var (pageManifest, pageHash) = await _chunkIngester.IngestAsync(content, mimeType);

                    pageManifestHashes.Add(new BlobHash(pageHash));
                    totalSize += pageManifest.FileSize;

                    entries.Add(new Shared.Models.ChapterFileEntry
                    {
                        Hash = pageHash,
                        Path = name,
                        Size = pageManifest.FileSize
                    });

                    // Announce page manifest and all chunks to DHT so the gateway can find them
                    await _dhtNode.StoreAsync(Convert.FromHexString(pageHash));
                    foreach (var chunkHash in pageManifest.Chunks)
                        await _dhtNode.StoreAsync(Convert.FromHexString(chunkHash));
                }
            }

            // Register series to get authoritative ID and title
            var (seriesId, seriesTitle) = await _seriesRegistry.RegisterSeriesAsync(request.Source, request.ExternalMangaId);

            var keyPair = await _keyStore.GetAsync();

            var title = request.DisplayName;
            if (!string.IsNullOrEmpty(seriesTitle) && !title.Contains(seriesTitle, StringComparison.OrdinalIgnoreCase))
            {
                if (title.Contains(request.ExternalMangaId))
                    title = title.Replace(request.ExternalMangaId, seriesTitle);
                else
                    title = $"{seriesTitle} {title}";
            }

            Shared.Models.ChapterManifest chapterManifest = new()
            {
                SchemaVersion = 2,
                ChapterNumber = request.ChapterNumber,
                CreatedUtc = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc),
                ChapterId = seriesId + ":" + request.ChapterNumber.ToString(),
                Language = request.Language,
                SeriesId = seriesId,
                ScanGroup = request.ScanlatorId,
                Title = title,
                TotalSize = totalSize,
                PublicKey = keyPair.PublicKeyBase64,
                SignedBy = "self",
                Files = entries
            };

            var hash = ManifestHash.FromManifest(chapterManifest);

            var isManifestExisting = await _manifestStore.ExistsAsync(hash);

            if (isManifestExisting)
                throw new InvalidOperationException("Manifest already exists");

            // Save unsigned manifest first
            await _manifestStore.SaveAsync(hash, chapterManifest);

            // Sign manifest
            byte[] privateKeyBytes = Convert.FromBase64String(keyPair.PrivateKeyBase64);
            var key = Key.Import(SignatureAlgorithm.Ed25519, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
            var signedManifest = _manifestSigning.SignManifest(chapterManifest, key);

            // Publish — challenge-response auth handled by TrackerPublisher
            var announceRequest = new Shared.Models.AnnounceManifestRequest
            {
                NodeId = Convert.ToHexString(_nodeIdentity.NodeId).ToLowerInvariant(),
                ManifestHash = hash,
                SchemaVersion = chapterManifest.SchemaVersion,
                SeriesId = chapterManifest.SeriesId,
                ChapterNumber = chapterManifest.ChapterNumber,
                Language = chapterManifest.Language,
                ReleaseType = request.ReleaseType,
                Source = request.Source,
                ExternalMangaId = request.ExternalMangaId,
                ChapterId = chapterManifest.ChapterId,
                Title = chapterManifest.Title,
                ScanGroup = chapterManifest.ScanGroup,
                TotalSize = chapterManifest.TotalSize,
                CreatedUtc = chapterManifest.CreatedUtc,
                Signature = signedManifest.Signature,
                PublicKey = signedManifest.PublisherPublicKey,
                SignedBy = chapterManifest.SignedBy,
                Files = (List<Shared.Models.ChapterFileEntry>)chapterManifest.Files
            };

            await _trackerPublisher.PublishManifestAsync(announceRequest, ct);

            // Re-save with signature
            chapterManifest = chapterManifest with { Signature = signedManifest.Signature };
            await _manifestStore.SaveAsync(hash, chapterManifest);

            // Announce to DHT so the gateway can discover this node as a provider
            await _dhtNode.StoreAsync(Convert.FromHexString(hash.Value));

            return new ImportChapterResult
            {
                ManifestHash = hash,
                FileCount = entries.Count,
                AlreadyExists = isManifestExisting
            };
        }

        public async Task ReannounceAsync(ManifestHash hash, string nodeId)
        {
            var manifest = await _manifestStore.GetAsync(hash);
            if (manifest == null)
                throw new FileNotFoundException($"Manifest {hash} not found");

            if (string.IsNullOrEmpty(manifest.Signature) || string.IsNullOrEmpty(manifest.PublicKey))
                throw new InvalidOperationException("Manifest does not contain signature data. Cannot re-announce.");

            await _trackerPublisher.PublishManifestAsync(new Shared.Models.AnnounceManifestRequest
            {
                NodeId = nodeId,
                ManifestHash = hash,
                SchemaVersion = manifest.SchemaVersion,
                SeriesId = manifest.SeriesId,
                ChapterNumber = manifest.ChapterNumber,
                Language = manifest.Language,
                ReleaseType = ReleaseType.VerifiedScanlation,
                ChapterId = manifest.ChapterId,
                Title = manifest.Title,
                ScanGroup = manifest.ScanGroup,
                TotalSize = manifest.TotalSize,
                CreatedUtc = manifest.CreatedUtc,
                Signature = manifest.Signature,
                PublicKey = manifest.PublicKey,
                Files = (List<Shared.Models.ChapterFileEntry>)manifest.Files
            });
        }
    }
}

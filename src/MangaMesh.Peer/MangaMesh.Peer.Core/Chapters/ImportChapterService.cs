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
using System.IO.Compression;

namespace MangaMesh.Peer.Core.Chapters
{
    public class ImportChapterService : IImportChapterService
    {
        private readonly IBlobStore _blobStore;
        private readonly IManifestStore _manifestStore;
        private readonly ITrackerClient _trackerClient;
        private readonly IKeyStore _keyStore;
        private readonly INodeIdentityService _nodeIdentity;
        private readonly IKeyPairService _keyPairService;
        private readonly IChunkIngester _chunkIngester;

        public ImportChapterService(
            IBlobStore blobStore,
            IManifestStore manifestStore,
            ITrackerClient trackerClient,
            IKeyStore keyStore,
            INodeIdentityService nodeIdentity,
            IKeyPairService keyPairService,
            IChunkIngester chunkIngester)
        {
            _blobStore = blobStore;
            _manifestStore = manifestStore;
            _trackerClient = trackerClient;
            _keyStore = keyStore;
            _nodeIdentity = nodeIdentity;
            _keyPairService = keyPairService;
            _chunkIngester = chunkIngester;
        }

        public async Task<ImportChapterResult> ImportAsync(ImportChapterRequest request, CancellationToken ct = default)
        {
            List<Shared.Models.ChapterFileEntry> entries = new();
            var pageManifestHashes = new List<BlobHash>();
            long totalSize = 0;

            if (Directory.Exists(request.SourceDirectory))
            {
                // Directory mode
                var files = Directory.GetFiles(request.SourceDirectory)
                    .Where(f => IsImageFile(f))
                    .OrderBy(f => f)
                    .ToArray();

                if (files.Length == 0)
                    throw new InvalidOperationException("No valid image files found in source folder.");

                foreach (var file in files)
                {
                    using var stream = File.OpenRead(file);
                    var mimeType = GetMimeType(file);

                    // ChunkIngester splits the file, stores chunks, creates PageManifest, stores PageManifest
                    var (pageManifest, pageHash) = await _chunkIngester.IngestAsync(stream, mimeType);

                    pageManifestHashes.Add(new BlobHash(pageHash));
                    totalSize += pageManifest.FileSize;

                    entries.Add(new Shared.Models.ChapterFileEntry
                    {
                        Hash = pageHash, // This is now the hash of the PageManifest
                        Path = Path.GetFileName(file),
                        Size = pageManifest.FileSize
                    });
                }
            }
            else if (File.Exists(request.SourceDirectory))
            {
                var ext = Path.GetExtension(request.SourceDirectory).ToLowerInvariant();
                if (ext == ".zip" || ext == ".cbz")
                {
                    // Zip mode
                    using var stream = File.OpenRead(request.SourceDirectory);
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

                    var zipEntries = archive.Entries
                        .Where(e => IsImageFile(e.FullName) && e.Length > 0)
                        .OrderBy(e => e.FullName)
                        .ToArray();

                    if (zipEntries.Length == 0)
                        throw new InvalidOperationException("No valid image files found in zip archive.");

                    foreach (var entry in zipEntries)
                    {
                        using var entryStream = entry.Open();
                        // We need to copy to a memory stream or let ChunkIngester handle non-seekable if it supports it.
                        // ChunkIngester likely needs seekable stream if it does chunking strategies, but let's check.
                        // Assuming ChunkIngester handles it, or we copy to MemoryStream. 
                        // To be safe and since pages are usually small, let's copy to MemoryStream if needed.
                        // Actually, standard ZipArchive entries are not seekable.
                        using var memStream = new MemoryStream();
                        await entryStream.CopyToAsync(memStream);
                        memStream.Position = 0;

                        var mimeType = GetMimeType(entry.Name);
                        var (pageManifest, pageHash) = await _chunkIngester.IngestAsync(memStream, mimeType);

                        pageManifestHashes.Add(new BlobHash(pageHash));
                        totalSize += pageManifest.FileSize;

                        entries.Add(new Shared.Models.ChapterFileEntry
                        {
                            Hash = pageHash,
                            Path = entry.Name, // Use simple name or full path? standard usually flat or relative. 
                                               // Let's use entry.Name (filename) to flatten, or entry.FullName if we want to preserve structure (but web readers often prefer flat).
                                               // Let's use Name for now.
                            Size = pageManifest.FileSize
                        });
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unsupported file type. Please provide a Directory, .zip, or .cbz file.");
                }
            }
            else
            {
                throw new DirectoryNotFoundException($"Source path not found: {request.SourceDirectory}");
            }

            // Step 2.1: Register Series to get authoritative ID and Title
            var (seriesId, seriesTitle) = await _trackerClient.RegisterSeriesAsync(request.Source, request.ExternalMangaId);


            // Step 3: Create Chapter Manifest (v2)
            var keyPair = await _keyStore.GetAsync();

            var title = request.DisplayName;
            if (!string.IsNullOrEmpty(seriesTitle) && !title.Contains(seriesTitle, StringComparison.OrdinalIgnoreCase))
            {
                if (title.Contains(request.ExternalMangaId))
                {
                    title = title.Replace(request.ExternalMangaId, seriesTitle);
                }
                else
                {
                    title = $"{seriesTitle} {title}";
                }
            }


            Shared.Models.ChapterManifest chapterManifest = new()
            {
                SchemaVersion = 2, // V2 for Chunk support
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

            if (!isManifestExisting)
            {
                // Step 4: save manifest
                await _manifestStore.SaveAsync(hash, chapterManifest);

                // Step 5: publish manifest to trackers

                byte[] privateKeyBytes = Convert.FromBase64String(keyPair.PrivateKeyBase64);

                var key = Key.Import(
                    SignatureAlgorithm.Ed25519,
                    privateKeyBytes,
                    KeyBlobFormat.RawPrivateKey);

                var signedManifest = ManifestSigningService.SignManifest(chapterManifest, key);

                // Step 5.2 publish to trackers
                var announceRequest = new Shared.Models.AnnounceManifestRequest
                {
                    NodeId = _nodeIdentity.NodeId,
                    ManifestHash = hash,
                    SchemaVersion = chapterManifest.SchemaVersion,
                    SeriesId = chapterManifest.SeriesId,
                    ChapterNumber = chapterManifest.ChapterNumber,
                    Language = chapterManifest.Language,
                    ReleaseType = request.ReleaseType,
                    Source = request.Source,
                    ExternalMangaId = request.ExternalMangaId,

                    // Added fields for verification
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

                await AnnounceWithRetryAsync(announceRequest);

                // Step 6: update manifest with signature details before returning/saving if needed

                // Re-save with signature
                chapterManifest = chapterManifest with
                {
                    Signature = signedManifest.Signature
                };

                await _manifestStore.SaveAsync(hash, chapterManifest);
            }

            // Step 6: return result
            return new ImportChapterResult
            {
                ManifestHash = hash,
                FileCount = entries.Count,
                AlreadyExists = isManifestExisting
            };
        }

        private static string GetMimeType(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        public async Task ReannounceAsync(ManifestHash hash, string nodeId)
        {
            var manifest = await _manifestStore.GetAsync(hash);
            if (manifest == null)
                throw new FileNotFoundException($"Manifest {hash} not found");

            if (string.IsNullOrEmpty(manifest.Signature) || string.IsNullOrEmpty(manifest.PublicKey))
                throw new InvalidOperationException("Manifest does not contain signature data. Cannot re-announce.");

            await AnnounceWithRetryAsync(new Shared.Models.AnnounceManifestRequest
            {
                NodeId = nodeId,
                ManifestHash = hash,
                SchemaVersion = manifest.SchemaVersion,
                SeriesId = manifest.SeriesId,
                ChapterNumber = manifest.ChapterNumber,
                Language = manifest.Language,
                ReleaseType = ReleaseType.VerifiedScanlation, // Assuming VerifiedScanlation for signed manifests

                // Verification fields
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

        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".webp";
        }

        private async Task AnnounceWithRetryAsync(Shared.Models.AnnounceManifestRequest request)
        {
            // 1. Get Identity Keys
            var keys = await _keyStore.GetAsync();
            if (keys == null)
            {
                throw new InvalidOperationException("Cannot announce manifest: No identity keys found.");
            }

            // 2. Request Challenge
            var challenge = await _trackerClient.CreateChallengeAsync(keys.PublicKeyBase64);

            // 3. Solve Challenge
            var signature = _keyPairService.SolveChallenge(challenge.Nonce, keys.PrivateKeyBase64);

            // 4. Authorize Manifest
            var authRequest = new Shared.Models.AuthorizeManifestRequest
            {
                ChallengeId = challenge.ChallengeId,
                SignatureBase64 = signature,
                ManifestHash = request.ManifestHash.Value,
                NodeId = request.NodeId,
                PublicKeyBase64 = keys.PublicKeyBase64
            };

            await _trackerClient.AuthorizeManifestAsync(authRequest);

            // 5. Announce Manifest (Tracker will check authorization)
            await _trackerClient.AnnounceManifestAsync(request);
        }
    }
}

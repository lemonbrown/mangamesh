extern alias Index;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using KeyPairResult = MangaMesh.Peer.Core.Keys.KeyPairResult;
using ChapterManifest = MangaMesh.Shared.Models.ChapterManifest;
using Microsoft.Extensions.DependencyInjection.Extensions;


using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Node;
using Index::MangaMesh.Index.Api.Models;
using Index::MangaMesh.Index.Api.Stores;
using Index::MangaMesh.Index.Api.Services;
using MangaMesh.Shared.Stores;
using MangaMesh.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MangaMesh.IntegrationTests
{
    [TestClass]
    public class ImportChapterTests : IndexIntegrationTestBase
    {
        private static readonly byte[] TestNodeIdBytes = System.Text.Encoding.UTF8.GetBytes("test-peer-node-id");
        private static readonly string TestNodeIdHex = Convert.ToHexString(TestNodeIdBytes).ToLowerInvariant();

        private Mock<IKeyStore> _mockKeyStore = null!;
        private Mock<INodeIdentity> _mockNodeIdentity = null!;
        private Mock<IBlobStore> _mockBlobStore = null!;
        private Mock<IManifestStore> _mockManifestStore = null!;
        private Mock<IMangaMetadataProvider> _mockMetadataProvider = null!;

        private IKeyPairService _keyPairService = null!;
        private IChunkIngester _chunkIngester = null!;
        private ImportChapterService _importService = null!;
        private TrackerClient _trackerClient = null!;

        private KeyPairResult _peerKeys = null!;

        [TestInitialize]
        public async Task Setup()
        {
            // 0. Mock Index Metadata Provider (to avoid external calls)
            _mockMetadataProvider = new Mock<IMangaMetadataProvider>();
            _mockMetadataProvider.Setup(p => p.GetMangaAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => new MangaMetadata
                {
                    ExternalMangaId = id,
                    Source = ExternalMetadataSource.AniList,
                    CanonicalTitle = "Mock Manga Title",
                    AltTitles = new List<string> { "Mock Manga Title" }
                });

            // Reconfigure Factory to use Mock Metadata Provider and an isolated in-memory ManifestEntryStore
            var mockManifestEntryStore = new Mock<IManifestEntryStore>();
            mockManifestEntryStore.Setup(s => s.GetAsync(It.IsAny<string>())).ReturnsAsync((ManifestEntry?)null);
            mockManifestEntryStore.Setup(s => s.AddAsync(It.IsAny<ManifestEntry>())).Returns(Task.CompletedTask);

            Factory = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IMangaMetadataProvider>();
                    services.AddSingleton(_mockMetadataProvider.Object);

                    services.RemoveAll<IManifestEntryStore>();
                    services.AddSingleton(mockManifestEntryStore.Object);
                });
            });
            // Re-create Client after re-configuring factory
            Client = Factory.CreateClient();

            _mockKeyStore = new Mock<IKeyStore>();

            // 1. Setup Peer Dependencies
            _keyPairService = new KeyPairService(_mockKeyStore.Object, NullLogger<MangaMesh.Peer.Core.Keys.KeyPairService>.Instance); // Real service
            _peerKeys = await _keyPairService.GenerateKeyPairBase64Async();

            _mockKeyStore.Setup(s => s.GetAsync()).ReturnsAsync(new PublicPrivateKeyPair { PublicKeyBase64 = _peerKeys.PublicKeyBase64, PrivateKeyBase64 = _peerKeys.PrivateKeyBase64 });

            _mockNodeIdentity = new Mock<INodeIdentity>();
            _mockNodeIdentity.Setup(s => s.NodeId).Returns(System.Text.Encoding.UTF8.GetBytes("test-peer-node-id"));

            _mockBlobStore = new Mock<IBlobStore>();
            // Setup BlobStore to "succeed" on writes
            _mockBlobStore.Setup(s => s.PutAsync(It.IsAny<Stream>())).ReturnsAsync(new BlobHash("dummy-hash"));

            _mockManifestStore = new Mock<IManifestStore>();
            _mockManifestStore.Setup(s => s.ExistsAsync(It.IsAny<ManifestHash>())).ReturnsAsync(false);
            _mockManifestStore.Setup(s => s.SaveAsync(It.IsAny<ManifestHash>(), It.IsAny<ChapterManifest>())).Returns(Task.CompletedTask);

            // Chunk Ingester (Mocking for simplicity in integration test, focus is on Index interaction)
            var mockChunkIngester = new Mock<IChunkIngester>();
            // Return valid tuple with PageManifest and Hash
            mockChunkIngester.Setup(i => i.IngestAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync((new PageManifest { FileSize = 100 }, Convert.ToHexString(new byte[16])));
            _chunkIngester = mockChunkIngester.Object;

            // 2. Setup TrackerClient pointing to Index
            _trackerClient = new TrackerClient(Client);

            // 3. Seed Index with Peer Public Key (User Requirement)
            using (var scope = Factory.Services.CreateScope())
            {
                var publicKeyStore = scope.ServiceProvider.GetRequiredService<IPublicKeyStore>();
                await publicKeyStore.StoreAsync(new PublicKeyRecord
                {
                    PublicKeyBase64 = _peerKeys.PublicKeyBase64,
                    RegisteredAt = DateTime.UtcNow
                });

                var nodeRegistry = scope.ServiceProvider.GetRequiredService<INodeRegistry>();
                nodeRegistry.RegisterOrUpdate(new TrackerNode
                {
                    NodeId = TestNodeIdHex,
                    LastSeen = DateTime.UtcNow
                });
            }

            // 4. Instantiate Service
            // TrackerPublisher encapsulates the challenge-response auth flow
            var trackerPublisher = new TrackerPublisher(
                _trackerClient,  // ITrackerChallengeClient
                _trackerClient,  // IManifestAnnouncer
                _mockKeyStore.Object,
                _keyPairService,
                NullLogger<TrackerPublisher>.Instance);

            var formatProvider = new DefaultImageFormatProvider();
            var sourceReaders = new IChapterSourceReader[]
            {
                new DirectorySourceReader(formatProvider),
                new ZipSourceReader(formatProvider)
            };

            _importService = new ImportChapterService(
                _mockBlobStore.Object,
                _mockManifestStore.Object,
                _trackerClient,  // ISeriesRegistry
                _mockKeyStore.Object,
                _mockNodeIdentity.Object,
                _chunkIngester,
                trackerPublisher,
                sourceReaders,
                formatProvider,
                new ManifestSigningService(),
                new Mock<IDhtNode>().Object,
                NullLogger<ImportChapterService>.Instance
            );
        }

        [TestMethod]
        public async Task ImportChapter_RegistersSeriesAndAnnouncesManifest()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), "MangaMeshTest_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create dummy files
                await File.WriteAllBytesAsync(Path.Combine(tempDir, "01.jpg"), new byte[10]);
                await File.WriteAllBytesAsync(Path.Combine(tempDir, "02.jpg"), new byte[10]);

                var request = new ImportChapterRequest
                {
                    SourceDirectory = tempDir,
                    DisplayName = "Chapter 1",
                    ChapterNumber = 1.0f,
                    Language = "en",
                    ScanlatorId = "TestGroup",
                    Source = ExternalMetadataSource.AniList,
                    ExternalMangaId = "12345",
                    ReleaseType = ReleaseType.VerifiedScanlation
                };

                // Act
                var result = await _importService.ImportAsync(request);

                // Assert
                Assert.IsNotNull(result);
                Assert.IsFalse(result.AlreadyExists);
                // Assert.IsNotNull(result.ManifestHash);

                // Verify Index State via Tracker Client
                // 1. Verify Series Registered
                // Import Service registers series with ID from Index.
                // We can search for it or check if we can get peers for the manifest.

                // 2. Verify Manifest Announced
                // GetPeersForManifestAsync should return our node
                var peers = await _trackerClient.GetPeersForManifestAsync(result.ManifestHash.Value);
                Assert.IsTrue(peers.Any(), "No peers found for manifest");
                Assert.IsTrue(peers.Any(p => p.NodeId == TestNodeIdHex), "Our node not found in peers");

            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}

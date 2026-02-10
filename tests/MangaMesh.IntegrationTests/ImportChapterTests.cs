extern alias Index;
using MangaMesh.Client.Blob;
using MangaMesh.Client.Chapters;
using MangaMesh.Client.Content;
using MangaMesh.Client.Keys;
using MangaMesh.Client.Manifests;
using MangaMesh.Client.Node;
using MangaMesh.Client.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Index::MangaMesh.Backend.Tracker.Models;
using Index::MangaMesh.Backend.Tracker.Stores;
using Index::MangaMesh.Backend.Tracker.Services;
using KeyPairResult = MangaMesh.Client.Keys.KeyPairResult;
using ChapterManifest = MangaMesh.Shared.Models.ChapterManifest;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Alias to avoid ambiguity with Tracker.Services.IMangaMetadataProvider
using IIndexMetadataProvider = Index::MangaMesh.Backend.Tracker.Services.IMangaMetadataProvider;

namespace MangaMesh.IntegrationTests
{
    [TestClass]
    public class ImportChapterTests : IndexIntegrationTestBase
    {
        private Mock<IKeyStore> _mockKeyStore;
        private Mock<INodeIdentityService> _mockNodeIdentity;
        private Mock<IBlobStore> _mockBlobStore;
        private Mock<IManifestStore> _mockManifestStore;
        private Mock<IIndexMetadataProvider> _mockMetadataProvider;

        private IKeyPairService _keyPairService;
        private IChunkIngester _chunkIngester;
        private ImportChapterService _importService;
        private TrackerClient _trackerClient;

        private KeyPairResult _peerKeys;

        [TestInitialize]
        public async Task Setup()
        {
            // 0. Mock Index Metadata Provider (to avoid external calls)
            _mockMetadataProvider = new Mock<IIndexMetadataProvider>();
            _mockMetadataProvider.Setup(p => p.GetMangaAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => new MangaMetadata 
                { 
                    ExternalMangaId = id, 
                    Source = ExternalMetadataSource.AniList,
                    CanonicalTitle = "Mock Manga Title",
                    AltTitles = new List<string> { "Mock Manga Title" }
                });

            // Reconfigure Factory to use Mock Metadata Provider
            Factory = Factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IIndexMetadataProvider>();
                    services.AddSingleton(_mockMetadataProvider.Object);
                });
            });
            // Re-create Client after re-configuring factory
            Client = Factory.CreateClient();

            _mockKeyStore = new Mock<IKeyStore>();
            
            // 1. Setup Peer Dependencies
            _keyPairService = new KeyPairService(_mockKeyStore.Object); // Real service
            _peerKeys = await _keyPairService.GenerateKeyPairBase64Async();

            _mockKeyStore.Setup(s => s.GetAsync()).ReturnsAsync(new PublicPrivateKeyPair { PublicKeyBase64 = _peerKeys.PublicKeyBase64, PrivateKeyBase64 = _peerKeys.PrivateKeyBase64 });

            _mockNodeIdentity = new Mock<INodeIdentityService>();
            _mockNodeIdentity.Setup(s => s.NodeId).Returns("test-peer-node-id");

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
                .ReturnsAsync((new PageManifest { FileSize = 100 }, "dummy-page-hash"));
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
            }

            // 4. Instantiate Service
            _importService = new ImportChapterService(
                _mockBlobStore.Object,
                _mockManifestStore.Object,
                _trackerClient,
                _mockKeyStore.Object,
                _mockNodeIdentity.Object,
                _keyPairService,
                _chunkIngester
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
                Assert.IsNotNull(result.ManifestHash);

                // Verify Index State via Tracker Client
                // 1. Verify Series Registered
                // Import Service registers series with ID from Index.
                // We can search for it or check if we can get peers for the manifest.

                // 2. Verify Manifest Announced
                // GetPeersForManifestAsync should return our node
                var peers = await _trackerClient.GetPeersForManifestAsync(result.ManifestHash.Value);
                Assert.IsTrue(peers.Any(), "No peers found for manifest");
                Assert.IsTrue(peers.Any(p => p.NodeId == "test-peer-node-id"), "Our node not found in peers");

            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}

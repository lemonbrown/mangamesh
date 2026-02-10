using MangaMesh.Shared.Models;
using Moq;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Keys;

namespace MangaMesh.Peer.Tests
{
    [TestClass]
    public class ImportChapterServiceTests
    {
        private Mock<IBlobStore> _mockBlobStore;
        private Mock<IManifestStore> _mockManifestStore;
        private Mock<ITrackerClient> _mockTrackerClient;
        private Mock<IKeyStore> _mockKeyStore;
        private Mock<INodeIdentityService> _mockNodeIdentity;
        private Mock<IKeyPairService> _mockKeyPairService;
        private Mock<IChunkIngester> _mockChunkIngester;
        private ImportChapterService _service;
        private string _tempDirectory;

        [TestInitialize]
        public void Setup()
        {
            _mockBlobStore = new Mock<IBlobStore>();
            _mockManifestStore = new Mock<IManifestStore>();
            _mockTrackerClient = new Mock<ITrackerClient>();
            _mockKeyStore = new Mock<IKeyStore>();
            _mockNodeIdentity = new Mock<INodeIdentityService>();
            _mockKeyPairService = new Mock<IKeyPairService>();
            _mockChunkIngester = new Mock<IChunkIngester>();

            _service = new ImportChapterService(
                _mockBlobStore.Object,
                _mockManifestStore.Object,
                _mockTrackerClient.Object,
                _mockKeyStore.Object,
                _mockNodeIdentity.Object,
                _mockKeyPairService.Object,
                _mockChunkIngester.Object);

            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [TestMethod]
        public async Task ImportAsync_ValidDirectory_CreatesAndAnnouncesManifest()
        {
            // Arrange
            // 1. Create dummy files
            var file1 = Path.Combine(_tempDirectory, "001.jpg");
            var file2 = Path.Combine(_tempDirectory, "002.png");
            File.WriteAllBytes(file1, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(file2, new byte[] { 4, 5, 6 });

            var request = new ImportChapterRequest
            {
                SourceDirectory = _tempDirectory,
                SeriesId = "series-123",
                ScanlatorId = "scan-group-1",
                Language = "en",
                ChapterNumber = 1.0f,
                ReleaseType = ReleaseType.VerifiedScanlation,
                DisplayName = "Chapter 1",
                Source = ExternalMetadataSource.MangaDex,
                ExternalMangaId = "ext-123"
            };

            // 2. Setup Mocks
            _mockChunkIngester.Setup(x => x.IngestAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync((Stream s, string m) => 
                {
                    // Return dummy page manifest and hash
                    var pm = new PageManifest { FileSize = s.Length };
                    var hash = "hash-" + Guid.NewGuid();
                    return (pm, hash);
                });

            _mockTrackerClient.Setup(x => x.RegisterSeriesAsync(It.IsAny<ExternalMetadataSource>(), It.IsAny<string>()))
                .ReturnsAsync(("series-123", "Test Series"));

            _mockKeyStore.Setup(x => x.GetAsync())
                .ReturnsAsync(new PublicPrivateKeyPair 
                { 
                    PublicKeyBase64 = "pub-key", 
                    PrivateKeyBase64 = Convert.ToBase64String(new byte[32]) // valid length dummy
                });

            _mockManifestStore.Setup(x => x.ExistsAsync(It.IsAny<ManifestHash>()))
                .ReturnsAsync(false);

            _mockNodeIdentity.Setup(x => x.NodeId).Returns("node-1");

            _mockTrackerClient.Setup(x => x.CreateChallengeAsync(It.IsAny<string>()))
                .ReturnsAsync(new Shared.Models.KeyChallengeResponse { ChallengeId = "chal-1", Nonce = "nonce-1" });

            _mockKeyPairService.Setup(x => x.SolveChallenge(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("signature-1");

            // Act
            var result = await _service.ImportAsync(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.AlreadyExists);
            Assert.AreEqual(2, result.FileCount);

            // Verify interactions
            _mockTrackerClient.Verify(x => x.RegisterSeriesAsync(ExternalMetadataSource.MangaDex, "ext-123"), Times.Once);
            
            _mockChunkIngester.Verify(x => x.IngestAsync(It.IsAny<Stream>(), "image/jpeg"), Times.Once); // for .jpg
            _mockChunkIngester.Verify(x => x.IngestAsync(It.IsAny<Stream>(), "image/png"), Times.Once); // for .png

            _mockManifestStore.Verify(x => x.SaveAsync(It.IsAny<ManifestHash>(), It.Is<ChapterManifest>(m =>
                m.ChapterNumber == 1.0f &&
                m.SeriesId == "series-123" &&
                m.Files.Count() == 2 &&
                string.IsNullOrEmpty(m.Signature) // Initial save is unsigned
            )), Times.Once);

            _mockManifestStore.Verify(x => x.SaveAsync(It.IsAny<ManifestHash>(), It.Is<ChapterManifest>(m =>
                m.ChapterNumber == 1.0f &&
                m.SeriesId == "series-123" &&
                m.Files.Count() == 2 &&
                !string.IsNullOrEmpty(m.Signature) // Final save is signed
            )), Times.Once);

            _mockTrackerClient.Verify(x => x.AnnounceManifestAsync(It.Is<Shared.Models.AnnounceManifestRequest>(req =>
                req.ChapterNumber == 1.0f &&
                !string.IsNullOrEmpty(req.Signature)
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ImportAsync_InvalidDirectory_ThrowsException()
        {
            var request = new ImportChapterRequest
            {
                SourceDirectory = Path.Combine(_tempDirectory, "non-existent"),
                SeriesId = "series-123"
            };

            bool threw = false;
            try
            {
                await _service.ImportAsync(request);
            }
            catch (DirectoryNotFoundException)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "Expected DirectoryNotFoundException");
        }

        [TestMethod]
        public async Task ImportAsync_NoImages_ThrowsException()
        {
            // Empty directory
            var request = new ImportChapterRequest
            {
                SourceDirectory = _tempDirectory,
                SeriesId = "series-123"
            };

            bool threw = false;
            try
            {
                await _service.ImportAsync(request);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            Assert.IsTrue(threw, "Expected InvalidOperationException");
        }
    }
}

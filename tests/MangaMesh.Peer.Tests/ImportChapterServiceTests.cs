using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Content;
// DefaultImageFormatProvider, DirectorySourceReader, ZipSourceReader are in MangaMesh.Peer.Core.Chapters
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Keys;
using AnnounceManifestRequest = MangaMesh.Shared.Models.AnnounceManifestRequest;

namespace MangaMesh.Peer.Tests
{
    [TestClass]
    public class ImportChapterServiceTests
    {
        private Mock<IBlobStore> _mockBlobStore = null!;
        private Mock<IManifestStore> _mockManifestStore = null!;
        private Mock<ISeriesRegistry> _mockSeriesRegistry = null!;
        private Mock<IKeyStore> _mockKeyStore = null!;
        private Mock<INodeIdentity> _mockNodeIdentity = null!;
        private Mock<IChunkIngester> _mockChunkIngester = null!;
        private Mock<ITrackerPublisher> _mockTrackerPublisher = null!;
        private ImportChapterService _service = null!;
        private string _tempDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockBlobStore = new Mock<IBlobStore>();
            _mockManifestStore = new Mock<IManifestStore>();
            _mockSeriesRegistry = new Mock<ISeriesRegistry>();
            _mockKeyStore = new Mock<IKeyStore>();
            _mockNodeIdentity = new Mock<INodeIdentity>();
            _mockChunkIngester = new Mock<IChunkIngester>();
            _mockTrackerPublisher = new Mock<ITrackerPublisher>();

            var formatProvider = new DefaultImageFormatProvider();
            var sourceReaders = new IChapterSourceReader[]
            {
                new DirectorySourceReader(formatProvider),
                new ZipSourceReader(formatProvider)
            };

            _service = new ImportChapterService(
                _mockBlobStore.Object,
                _mockManifestStore.Object,
                _mockSeriesRegistry.Object,
                _mockKeyStore.Object,
                _mockNodeIdentity.Object,
                _mockChunkIngester.Object,
                _mockTrackerPublisher.Object,
                sourceReaders,
                formatProvider,
                new ManifestSigningService(),
                new Mock<IDhtNode>().Object,
                NullLogger<ImportChapterService>.Instance);

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

            _mockChunkIngester.Setup(x => x.IngestAsync(It.IsAny<Stream>(), It.IsAny<string>()))
                .ReturnsAsync((Stream s, string m) =>
                {
                    var pm = new PageManifest { FileSize = s.Length };
                    var hash = Convert.ToHexString(Guid.NewGuid().ToByteArray() /* 16 bytes = 32 hex chars */);
                    return (pm, hash);
                });

            _mockSeriesRegistry.Setup(x => x.RegisterSeriesAsync(It.IsAny<ExternalMetadataSource>(), It.IsAny<string>()))
                .ReturnsAsync(("series-123", "Test Series"));

            _mockKeyStore.Setup(x => x.GetAsync())
                .ReturnsAsync(new PublicPrivateKeyPair
                {
                    PublicKeyBase64 = "pub-key",
                    PrivateKeyBase64 = Convert.ToBase64String(new byte[32]) // valid-length Ed25519 seed
                });

            _mockManifestStore.Setup(x => x.ExistsAsync(It.IsAny<ManifestHash>()))
                .ReturnsAsync(false);

            _mockNodeIdentity.Setup(x => x.NodeId).Returns(new byte[] { 1, 2, 3 });

            _mockTrackerPublisher.Setup(x => x.PublishManifestAsync(It.IsAny<AnnounceManifestRequest>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _service.ImportAsync(request);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.AlreadyExists);
            Assert.AreEqual(2, result.FileCount);

            // Verify series registration went to the correct interface
            _mockSeriesRegistry.Verify(x => x.RegisterSeriesAsync(ExternalMetadataSource.MangaDex, "ext-123"), Times.Once);

            _mockChunkIngester.Verify(x => x.IngestAsync(It.IsAny<Stream>(), "image/jpeg"), Times.Once);
            _mockChunkIngester.Verify(x => x.IngestAsync(It.IsAny<Stream>(), "image/png"), Times.Once);

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

            // Verify TrackerPublisher was called with a fully-formed announce request
            _mockTrackerPublisher.Verify(x => x.PublishManifestAsync(It.Is<AnnounceManifestRequest>(req =>
                req.NodeId == "010203" &&
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

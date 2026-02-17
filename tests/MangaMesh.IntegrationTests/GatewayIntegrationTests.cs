extern alias GatewayApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;
using System.Text;
using Moq;
using Microsoft.EntityFrameworkCore;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Data;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Tracker;
using GatewayApi::MangaMesh.Peer.GatewayApi.Config;

namespace MangaMesh.IntegrationTests
{
    [TestClass]
    public class GatewayIntegrationTests
    {
        // ... (lines 24-100 irrelevant for replacement context if I narrow it down, but I'll focus on imports and constructor)
        // I'll do two replaces. One for imports, one for constructor.

        private WebApplicationFactory<GatewayApi::Program> _factory = null!;
        private HttpClient _client = null!;
        private IDhtNode _gatewayNode = null!;
        private DhtNode _peerNode = null!;
        private TcpTransport _peerTransport = null!;
        private int _gatewayPort;
        private int _peerPort;

        private Mock<IBlobStore> _mockBlobStore = null!;

        [TestInitialize]
        public async Task Setup()
        {
            _gatewayPort = GetFreePort();
            _peerPort = GetFreePort();

            // Setup Gateway (Node A) via WebApplicationFactory
            _factory = new WebApplicationFactory<GatewayApi::Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Override Gateway Config to set a specific port
                        var config = services.FirstOrDefault(d => d.ServiceType == typeof(GatewayConfig));
                        if (config != null) services.Remove(config);

                        services.AddSingleton(new GatewayConfig
                        {
                            Enabled = true,
                            Port = _gatewayPort,
                            CacheTtlMinutes = 5
                        });

                        // Aggressive removal of all ClientDbContext related services
                        var dbContextType = typeof(ClientDbContext);
                        var servicesToRemove = services.Where(d =>
                            d.ServiceType == dbContextType ||
                            d.ServiceType == typeof(DbContextOptions) ||
                            (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(dbContextType))
                        ).ToList();

                        foreach (var descriptor in servicesToRemove)
                        {
                            services.Remove(descriptor);
                        }

                        services.AddDbContext<ClientDbContext>(options =>
                            options.UseInMemoryDatabase("GatewayTestDb_" + Guid.NewGuid()));
                    });
                });

            _client = _factory.CreateClient(); // This starts the app

            var mockKeyPairService = new Mock<IKeyPairService>();
            // Valid 32-byte Ed25519 private key (base64) and corresponding public key
            // Generated for testing purposes
            // Valid 32-byte Ed25519 private key (base64) and corresponding public key
            // Generated for testing purposes
            // var validPrivateKeyBase64 = "MC4CAQAwBQYDK2VwBCIEIN5fTqXzn8Qf7r7r7r7r7r7r7r7r7r7r7r7r7r7r7r7r"; // invalid simplistic, need real 32 bytes
            // Let's use a simpler known 32-byte key: 32 bytes of 'a'
            var dummyKey32Bytes = new byte[32];
            for (int i = 0; i < 32; i++) dummyKey32Bytes[i] = (byte)i;
            var validKeyBase64 = Convert.ToBase64String(dummyKey32Bytes);

            mockKeyPairService.Setup(s => s.GenerateKeyPairBase64Async())
                .ReturnsAsync(new KeyPairResult(validKeyBase64, validKeyBase64)); // Use same for pub/priv for valid length test

            // Get access to the Gateway's internal DhtNode for verification/bootstrapping
            _gatewayNode = _factory.Services.GetRequiredService<IDhtNode>();

            // Setup Peer Node (Node B)
            // We need a real DHT node running to respond to the Gateway
            _peerTransport = new TcpTransport(_peerPort);
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockKeyStore = new Mock<IKeyStore>();

            var identity = new NodeIdentity(mockKeyPairService.Object, mockConfig.Object, mockKeyStore.Object);
            //await identity.InitializeAsync();

            var storage = new InMemoryDhtStorage(); // Using Client internal storage if accessible? No, likely public or we use same trick
                                                    // InMemoryDhtStorage is in MangaMesh.Client.Node namespace

            var mockTracker = new Mock<ITrackerClient>();
            var connectionInfo = new ConsoleNodeConnectionInfoProvider();

            _peerNode = new DhtNode(identity, _peerTransport, storage, mockKeyPairService.Object, mockKeyStore.Object, mockTracker.Object, connectionInfo);

            // Wire up protocol handlers for Peer Node
            var router = new ProtocolRouter();
            var dhtHandler = new DhtProtocolHandler(_peerNode);
            router.Register(dhtHandler);

            // Content Handler for Peer
            _mockBlobStore = new Mock<IBlobStore>();
            _mockBlobStore.Setup(s => s.Exists(It.Is<BlobHash>(h => h.Value == "test-hash"))).Returns(true);
            _mockBlobStore.Setup(s => s.OpenReadAsync(It.Is<BlobHash>(h => h.Value == "test-hash")))
                .ReturnsAsync(() => new MemoryStream(Encoding.UTF8.GetBytes("{\"title\":\"Test Series\"}")));

            var contentHandler = new ContentProtocolHandler(_peerTransport, _mockBlobStore.Object);
            contentHandler.DhtNode = _peerNode;
            router.Register(contentHandler);

            _peerTransport.OnMessage += router.RouteAsync;

            _peerNode.StartWithMaintenance(enableBootstrap: false);

            // Bootstrap Peer to Gateway
            // Gateway is at 127.0.0.1:_gatewayPort
            // We need to ensure Gateway is running and listening on TCP
            // The GatewayService starts DhtNode.StartWithMaintenance, which opens TcpTransport
        }

        [TestCleanup]
        public void Cleanup()
        {
            _peerNode?.StopWithMaintenance();
            _factory?.Dispose();
        }

        [TestMethod]
        public async Task TestGatewayFetchesManifestFromPeer()
        {
            // ... (Existing Test Content) ...
            // 1. Peer announces content
            var manifestContent = Encoding.UTF8.GetBytes("{\"title\":\"Test Series\"}");
            var manifestHash = "test-hash";

            // Store locally on peer so it can answer GetManifest
            // (ContentProtocolHandler above handles the response)

            // We also need Peer to be findable by Gateway.
            // Bootstrap Peer -> Gateway
            var gatewayAddress = new NodeAddress("127.0.0.1", _gatewayPort);

            // Let's force Peer into Gateway's routing table via Ping
            // Gateway needs to be running.
            // Retry loop to wait for Gateway to start listening
            bool connected = false;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    await _peerNode.PingAsync(new RoutingEntry { Address = gatewayAddress, NodeId = _gatewayNode.Identity.NodeId }); // Ping to introduce
                    connected = true;
                    break;
                }
                catch (Exception)
                {
                    await Task.Delay(500);
                }
            }
            Assert.IsTrue(connected, "Failed to connect to Gateway after retries");
            await Task.Delay(1000); // Wait for Gateway start                       

            // Problem: We might not know Gateway's NodeID easily if it's generated on fly.
            // _gatewayNode.Identity.NodeId should be available.

            var entry = new RoutingEntry
            {
                Address = gatewayAddress,
                NodeId = _gatewayNode.Identity.NodeId
            };

            await _peerNode.PingAsync(entry);

            // Give time for Ping to complete and RT update
            await Task.Delay(500);

            // Now Peer should be in Gateway's RoutingTable.

            // So: Peer must Store "test-hash" on Gateway.
            var hashBytes = Encoding.UTF8.GetBytes(manifestHash);
            await _peerNode.StoreAsync(hashBytes);

            await Task.Delay(500); // Wait for Store to propagate

            // 2. Gateway API Request
            // GET /api/manifests/test-hash
            // Note: GatewayService.GetManifestAsync uses "manifest:{hash}" cache key.
            // The GatewayService.GetManifestAsync sends GetManifest request.
            // Our ContentProtocolHandler handles GetManifest and uses mockBlobStore (which we primed with "test-hash").

            //Wait, GatewayService.GetManifestAsync calls FindValueWithAddressAsync(dhtKey).
            //Peer responds with ITSELF as provider (if it has it? No, StoreAsync just stores matches on DHT).
            //Setup: Peer needs to tell Gateway "I have this content".
            //StoreAsync does that.

            var response = await _client.GetAsync($"/api/manifests/{manifestHash}");
            // API route might be different? Program.cs MapControllers uses attribute routing.
            // GatewayApi usually has Controllers. Where is ManifestController?
            // "api/manifests" suggests a ManifestController.
            // I created ContentController with "content/blob" and "content/file".
            // Existing test uses "/api/manifests".

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GetManifest failed: {response.StatusCode} {err}");
            }

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            var manifest = JsonSerializer.Deserialize<ManifestData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.IsNotNull(manifest);
            Assert.AreEqual(manifestHash, manifest.ContentHash);

            var contentStr = Encoding.UTF8.GetString(manifest.Data);
            StringAssert.Contains(contentStr, "Test Series");
        }

        [TestMethod]
        public async Task TestGatewayFetchesPageManifestAndReassembles()
        {
            // 1. Setup Test Data (300KB file)
            var fileSize = 300 * 1024;
            var fileContent = new byte[fileSize];
            for (int i = 0; i < fileSize; i++) fileContent[i] = (byte)(i % 256);

            // Chunk it (256KB + 44KB)
            var chunkSize = 262144;
            var chunk1 = new byte[chunkSize];
            var chunk2 = new byte[fileSize - chunkSize];
            Array.Copy(fileContent, 0, chunk1, 0, chunkSize);
            Array.Copy(fileContent, chunkSize, chunk2, 0, chunk2.Length);

            using var sha = System.Security.Cryptography.SHA256.Create();
            var h1 = Convert.ToHexString(sha.ComputeHash(chunk1)).ToLowerInvariant();
            var h2 = Convert.ToHexString(sha.ComputeHash(chunk2)).ToLowerInvariant();

            var pageManifest = new MangaMesh.Shared.Models.PageManifest
            {
                Version = 1,
                MimeType = "application/octet-stream",
                FileSize = fileSize,
                ChunkSize = chunkSize,
                Chunks = new List<string> { h1, h2 }
            };

            var pageManifestJson = JsonSerializer.Serialize(pageManifest);
            var pageManifestBytes = Encoding.UTF8.GetBytes(pageManifestJson);
            var pageHash = Convert.ToHexString(sha.ComputeHash(pageManifestBytes)).ToLowerInvariant();

            // 2. Setup Peer MockBlobStore to serve these

            // Serve PageManifest
            _mockBlobStore.Setup(s => s.Exists(It.Is<BlobHash>(h => h.Value == pageHash))).Returns(true);
            _mockBlobStore.Setup(s => s.OpenReadAsync(It.Is<BlobHash>(h => h.Value == pageHash)))
                .ReturnsAsync(() => new MemoryStream(pageManifestBytes));

            // Serve Chunks
            _mockBlobStore.Setup(s => s.Exists(It.Is<BlobHash>(h => h.Value == h1))).Returns(true);
            _mockBlobStore.Setup(s => s.OpenReadAsync(It.Is<BlobHash>(h => h.Value == h1)))
                .ReturnsAsync(() => new MemoryStream(chunk1));

            _mockBlobStore.Setup(s => s.Exists(It.Is<BlobHash>(h => h.Value == h2))).Returns(true);
            _mockBlobStore.Setup(s => s.OpenReadAsync(It.Is<BlobHash>(h => h.Value == h2)))
                .ReturnsAsync(() => new MemoryStream(chunk2));

            // 3. Announce to Gateway (Peer stores hash on Gateway)
            // Bootstrap Peer -> Gateway logic is inside Setup(), but we need to ensure connectivity.
            // Setup() does NOT bootstrap logic. It initializes nodes.
            // TestGatewayFetchesManifestFromPeer had logic to connect. We need to copy that or refactor.
            // For now, copy connectivity logic.

            var gatewayAddress = new NodeAddress("127.0.0.1", _gatewayPort);
            bool connected = false;
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    await _peerNode.PingAsync(new RoutingEntry { Address = gatewayAddress, NodeId = _gatewayNode.Identity.NodeId });
                    connected = true;
                    break;
                }
                catch (Exception)
                {
                    await Task.Delay(500);
                }
            }
            Assert.IsTrue(connected, "Failed to connect to Gateway");
            await Task.Delay(500);

            // Announce PageHash (so Gateway can find Peer)
            // Key = PageHash (converted to bytes logic in GatewayService might be HEX or UTF8?)
            // GatewayService: 
            // try { dhtKey = Convert.FromHexString(hash); }
            // So Gateway expects Hex String as key for DHT lookup if possible.
            // Peer StoreAsync takes byte[] key.
            // If Peer stores Convert.FromHexString(pageHash), then Gateway find matches.

            var keyBytes = Convert.FromHexString(pageHash);
            await _peerNode.StoreAsync(keyBytes);

            // Announce Chunks too!
            await _peerNode.StoreAsync(Convert.FromHexString(h1));
            await _peerNode.StoreAsync(Convert.FromHexString(h2));

            await Task.Delay(1000); // Wait for propagation

            // 4. Request via Gateway API
            // content/file/{pageHash}
            var response = await _client.GetAsync($"/content/file/{pageHash}");

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var err = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GetFile failed: {response.StatusCode} {err}");
            }

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var responseData = await response.Content.ReadAsByteArrayAsync();
            Assert.AreEqual(fileSize, responseData.Length);

            // Verify content match
            for (int i = 0; i < fileSize; i++)
            {
                if (fileContent[i] != responseData[i])
                    Assert.Fail($"Content mismatch at index {i}");
            }
        }

        private static int GetFreePort()
        {
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }
    }
}

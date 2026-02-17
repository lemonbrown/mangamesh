using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Storage;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Shared.Models;
using MangaMesh.Shared.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;

namespace MangaMesh.Peer.Dht.TestHarness
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("MangaMesh DHT Test Harness");
            Console.WriteLine("Commands:");
            Console.WriteLine("  start <port>                     - Start a new node");
            Console.WriteLine("  bootstrap <port> <host> <rport>  - Bootstrap node at <port> to <host>:<rport>");
            Console.WriteLine("  store <port> <value>             - Store value from node at <port>");
            Console.WriteLine("  get <port> <value>               - Find value from node at <port> (hashing value)");
            Console.WriteLine("  find <port> <target_port>        - Find node <target_port>'s ID from <port>");
            Console.WriteLine("  list                             - List active nodes");
            Console.WriteLine("  info <port>                      - Show node info");
            Console.WriteLine("  upload <path> <series_name> <chapter_number> [scan_group] - Upload chapter to Index");
            Console.WriteLine("  set-key <public_base64> <private_base64> - Set publisher identity keys");
            Console.WriteLine("  show-key                         - Show publisher identity keys");
            Console.WriteLine("  quit                             - Exit");

            var manager = new NodeManager();

            // Setup services for Upload
            var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
            var blobDir = Path.Combine(dataDir, "blobs");
            var manifestDir = Path.Combine(dataDir, "manifests");

            Directory.CreateDirectory(blobDir);
            Directory.CreateDirectory(manifestDir);

            var manifestStore = new ManifestStore(manifestDir);
            var storageMonitor = new StorageMonitorService(blobDir, manifestStore);
            var blobStore = new BlobStore(blobDir, storageMonitor);
            var chunkIngester = new ChunkIngester(blobStore);

            // Use persistent key store for Publisher Identity
            var keyPath = Path.Combine(dataDir, "publisher_keys.json");
            Console.WriteLine($"Publisher Keys Path: {keyPath}");

            var keyStore = new FileKeyStore(keyPath);
            var keyPairService = new KeyPairService(keyStore);

            // Create a consistent identity for the harness publisher
            INodeIdentityService harnessIdentity = new SimpleNodeIdentity();
            var identityConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            INodeIdentity cryptoIdentity = new NodeIdentity(keyPairService, identityConfig, keyStore);

            // Setup Tracker Client (Index API)
            // Assuming Index API is running on localhost:5176 based on previous session, or 5243 default
            var trackerHttp = new HttpClient { BaseAddress = new Uri("http://localhost:5176") };
            var trackerClient = new TrackerClient(trackerHttp);

            var importService = new ImportChapterService(
                blobStore,
                manifestStore,
                trackerClient,
                keyStore,
                cryptoIdentity,
                keyPairService,
                chunkIngester
            );

            // Initialize keys for signing
            var keys = await keyStore.GetAsync();
            if (keys == null)
            {
                var newKeys = await keyPairService.GenerateKeyPairBase64Async();
                Console.WriteLine($"Initialized new Publisher Keys. Public Key: {newKeys.PublicKeyBase64}");
            }
            else
            {
                Console.WriteLine($"Loaded Publisher Keys. Public Key: {keys.PublicKeyBase64}");
            }

            // Setup local MangaDex provider for search (since Index doesn't support searching external yet)
            var mdHttp = new HttpClient { BaseAddress = new Uri("https://api.mangadex.org") };
            mdHttp.DefaultRequestHeaders.UserAgent.ParseAdd("MangaMesh-TestHarness/1.0");
            var mdProvider = new MangaDexMetadataProvider(mdHttp, "lemonbrown", "QGBq2Wi2JDrHfKR");

            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = ParseCommand(line);
                if (parts.Count == 0) continue;

                var cmd = parts[0].ToLower();

                try
                {
                    switch (cmd)
                    {
                        case "quit":
                            return;
                        case "start":
                            manager.StartNode(int.Parse(parts[1]));
                            break;
                        case "bootstrap":
                            manager.Bootstrap(int.Parse(parts[1]), parts[2], int.Parse(parts[3]));
                            break;
                        case "store":
                            manager.Store(int.Parse(parts[1]), parts[2]);
                            break;
                        case "get":
                            manager.Get(int.Parse(parts[1]), parts[2]);
                            break;
                        case "find":
                            manager.Find(int.Parse(parts[1]), int.Parse(parts[2]));
                            break;
                        case "list":
                            manager.ListNodes();
                            break;
                        case "info":
                            manager.Info(int.Parse(parts[1]));
                            break;
                        case "upload":
                            if (parts.Count < 4)
                            {
                                Console.WriteLine("Usage: upload <path> <series_name> <chapter_number> [scan_group]");
                                break;
                            }
                            var path = parts[1];
                            var seriesName = parts[2];
                            var chapterNum = double.Parse(parts[3]);
                            var scanGroup = parts.Count > 4 ? parts[4] : "TestGroup";

                            await HandleUpload(importService, mdProvider, trackerClient, harnessIdentity, path, seriesName, chapterNum, scanGroup);
                            break;
                        case "set-key":
                            if (parts.Count < 3)
                            {
                                Console.WriteLine("Usage: set-key <public_base64> <private_base64>");
                                break;
                            }
                            await keyStore.SaveAsync(parts[1], parts[2]);
                            Console.WriteLine("Keys saved. Please restart the application for changes to take effect.");
                            break;
                        case "show-key":
                            var currentKeys = await keyStore.GetAsync();
                            if (currentKeys == null)
                            {
                                Console.WriteLine("No keys set.");
                            }
                            else
                            {
                                Console.WriteLine($"Public Key: {currentKeys.PublicKeyBase64}");
                                Console.WriteLine($"Private Key: {currentKeys.PrivateKeyBase64}");
                            }
                            break;
                        case "test-ser":
                            manager.TestSer();
                            break;
                        default:
                            Console.WriteLine("Unknown command");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
            }
        }

        private static List<string> ParseCommand(string line)
        {
            var parts = new List<string>();
            var currentPart = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (currentPart.Length > 0)
                    {
                        parts.Add(currentPart.ToString());
                        currentPart.Clear();
                    }
                }
                else
                {
                    currentPart.Append(c);
                }
            }

            if (currentPart.Length > 0)
            {
                parts.Add(currentPart.ToString());
            }

            return parts;
        }

        private static async Task HandleUpload(
            ImportChapterService importService,
            MangaDexMetadataProvider metadataProvider,
            ITrackerClient trackerClient,
            INodeIdentityService nodeIdentity,
            string path,
            string seriesName,
            double chapterNumber,
            string scanGroup)
        {
            // Ensure node is registered
            Console.WriteLine("Checking if node is registered with Tracker...");
            if (!await trackerClient.CheckNodeExistsAsync(nodeIdentity.NodeId))
            {
                Console.WriteLine($"Node {nodeIdentity.NodeId} not found on Tracker. Registering...");
                await trackerClient.AnnounceAsync(new Shared.Models.AnnounceRequest(nodeIdentity.NodeId, new List<string>()));
                Console.WriteLine("Node registered successfully.");
            }

            Console.WriteLine($"Searching for series '{seriesName}' on MangaDex...");
            var results = await metadataProvider.SearchMangaAsync(seriesName);
            var match = results.FirstOrDefault(); // Simple "First match" logic

            if (match == null)
            {
                Console.WriteLine("No series found with that name.");
                return;
            }

            Console.WriteLine($"Found Series: {match.Title} (ID: {match.ExternalMangaId}, Source: {match.Source})");

            var request = new ImportChapterRequest
            {
                SourceDirectory = path,
                ExternalMangaId = match.ExternalMangaId,
                Source = match.Source,
                ChapterNumber = chapterNumber,
                DisplayName = "", // Auto-generate
                Language = "en",
                ReleaseType = ReleaseType.VerifiedScanlation,
                ScanlatorId = scanGroup
            };

            Console.WriteLine("Starting import...");
            var result = await importService.ImportAsync(request);
            Console.WriteLine($"Import Complete!");
            Console.WriteLine($"Manifest Hash: {result.ManifestHash}");
            Console.WriteLine($"File Count: {result.FileCount}");
            Console.WriteLine($"Already Existed: {result.AlreadyExists}");
        }
    }

    public class NodeManager
    {
        private Dictionary<int, DhtNode> _nodes = new();
        private Dictionary<int, ITransport> _transports = new();

        public void StartNode(int port)
        {
            if (_nodes.ContainsKey(port))
            {
                Console.WriteLine($"Node at {port} already exists.");
                return;
            }

            var storage = new InMemoryDhtStorage();
            var keyStore = new InMemoryKeyStore();
            var keyPairService = new KeyPairService(keyStore);

            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            var identity = new NodeIdentity(keyPairService, config, keyStore);
            var transport = new TcpTransport(port);

            var tracker = new TrackerMock();
            var connectionInfo = new ConsoleNodeConnectionInfoProvider();

            var node = new DhtNode(identity, transport, storage, keyPairService, keyStore, tracker, connectionInfo);

            // Wiring for Protocol Multiplexing
            var router = new ProtocolRouter();
            var dhtHandler = new DhtProtocolHandler(node);
            router.Register(dhtHandler);
            transport.OnMessage += router.RouteAsync;

            node.StartWithMaintenance(enableBootstrap: false);

            _nodes[port] = node;
            _transports[port] = transport;
            Console.WriteLine($"Started node at port {port}. ID: {Convert.ToHexString(node.Identity.NodeId)}");
        }

        public void TestSer()
        {
            var msg = new MangaMesh.Peer.Core.Transport.DhtMessage 
            { 
                SenderNodeId = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, 
                Type = MangaMesh.Peer.Core.Transport.DhtMessageType.Ping 
            };
            var json = System.Text.Json.JsonSerializer.Serialize(msg);
            Console.WriteLine($"Serialized: {json}");
            
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<MangaMesh.Peer.Core.Transport.DhtMessage>(json);
            Console.WriteLine($"Deserialized: {Convert.ToHexString(deserialized.SenderNodeId)}");
        }

        public void Bootstrap(int port, string host, int remotePort)
        {
            if (!_nodes.TryGetValue(port, out var node))
            {
                Console.WriteLine($"Node {port} not found. Start it first.");
                return;
            }

            var entry = new RoutingEntry
            {
                Address = new NodeAddress(host, remotePort)
            };

            Console.WriteLine($"Bootstrapping {port} -> {host}:{remotePort}...");
            try
            {
                node.BootstrapAsync(new[] { entry }).Wait();
                Console.WriteLine("Bootstrap initiated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bootstrap failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public void Store(int port, string value)
        {
            if (!_nodes.TryGetValue(port, out var node))
            {
                Console.WriteLine($"Node {port} not found. Start it first.");
                return;
            }
            var hash = Crypto.Sha256(System.Text.Encoding.UTF8.GetBytes(value));
            Console.WriteLine($"Storing hash {Convert.ToHexString(hash)}...");
            try
            {
                node.StoreAsync(hash).Wait();
                Console.WriteLine("Store initiated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Store failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public void Get(int port, string value)
        {
            if (!_nodes.TryGetValue(port, out var node))
            {
                Console.WriteLine($"Node {port} not found. Start it first.");
                return;
            }
            var hash = Crypto.Sha256(System.Text.Encoding.UTF8.GetBytes(value));
            Console.WriteLine($"Searching for hash {Convert.ToHexString(hash)}...");
            try
            {
                var result = node.FindValueAsync(hash).Result;
                Console.WriteLine($"Found {result.Count} providers.");
                foreach (var r in result)
                {
                    Console.WriteLine($"- {Convert.ToHexString(r)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public void Find(int port, int targetPort)
        {
            if (!_nodes.TryGetValue(port, out var node)) return;
            if (!_nodes.TryGetValue(targetPort, out var targetNode))
            {
                Console.WriteLine("Target node not found locally to get ID.");
                return;
            }

            var targetId = targetNode.Identity.NodeId;
            Console.WriteLine($"Looking for node {Convert.ToHexString(targetId)} from {port}...");
            try
            {
                var result = node.FindNodeAsync(targetId).Result;
                Console.WriteLine($"Found {result.Count} closest nodes.");
                foreach (var n in result)
                {
                    Console.WriteLine($"- {n.Address.Host}:{n.Address.Port} ({Convert.ToHexString(n.NodeId)})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Find failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        public void ListNodes()
        {
            foreach (var kvp in _nodes)
            {
                Console.WriteLine($"Port: {kvp.Key}, ID: {Convert.ToHexString(kvp.Value.Identity.NodeId).Substring(0, 10)}...");
            }
        }

        public void Info(int port)
        {
            if (!_nodes.TryGetValue(port, out var node))
            {
                Console.WriteLine($"Node {port} not found.");
                return;
            }
            Console.WriteLine($"Node {port}");
            Console.WriteLine($"ID: {Convert.ToHexString(node.Identity.NodeId)}");
            int count = 0;
            foreach (var b in node.RoutingTable) count += b.Entries.Count;
            Console.WriteLine($"Routing Table Size: {count} peers");
            foreach (var b in node.RoutingTable)
            {
                foreach (var e in b.Entries)
                {
                    Console.WriteLine($" - {e.Address.Host}:{e.Address.Port}");
                }
            }
        }

    }

    public class FileKeyStore : IKeyStore
    {
        private readonly string _path;
        public FileKeyStore(string path) => _path = path;

        public async Task<PublicPrivateKeyPair?> GetAsync()
        {
            if (!File.Exists(_path)) return null;
            var json = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<PublicPrivateKeyPair>(json);
        }

        public async Task SaveAsync(string pub, string priv)
        {
            var pair = new PublicPrivateKeyPair { PublicKeyBase64 = pub, PrivateKeyBase64 = priv };
            var json = JsonSerializer.Serialize(pair);
            await File.WriteAllTextAsync(_path, json);
        }
    }

    public class InMemoryKeyStore : IKeyStore
    {
        private PublicPrivateKeyPair? _pair;

        public Task<PublicPrivateKeyPair?> GetAsync()
        {
            return Task.FromResult(_pair);
        }

        public Task SaveAsync(string publicKeyBase64, string privateKeyBase64)
        {
            _pair = new PublicPrivateKeyPair
            {
                PublicKeyBase64 = publicKeyBase64,
                PrivateKeyBase64 = privateKeyBase64
            };
            return Task.CompletedTask;
        }
    }

    public class SimpleNodeIdentity : INodeIdentityService
    {
        public string NodeId { get; } = Guid.NewGuid().ToString("N");

        public bool IsConnected { get; private set; } = true;

        public DateTime? LastPingUtc { get; private set; } = DateTime.UtcNow;

        public void UpdateStatus(bool isConnected)
        {
            IsConnected = isConnected;
            if (isConnected) LastPingUtc = DateTime.UtcNow;
        }
    }
}

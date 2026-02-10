using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using System.Net;

namespace MangaMesh.Peer.Dht.TestHarness
{
    internal class Program
    {
        static void Main(string[] args)
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
            Console.WriteLine("  quit                             - Exit");

            var manager = new NodeManager();

            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(' ');
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
                        default:
                            Console.WriteLine("Unknown command");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
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
            
            var identity = new NodeIdentity(keyPairService);
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
                foreach(var r in result)
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
            foreach(var b in node.RoutingTable) count += b.Entries.Count;
            Console.WriteLine($"Routing Table Size: {count} peers");
            foreach(var b in node.RoutingTable)
            {
                foreach(var e in b.Entries)
                {
                    Console.WriteLine($" - {e.Address.Host}:{e.Address.Port}");
                }
            }
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
}

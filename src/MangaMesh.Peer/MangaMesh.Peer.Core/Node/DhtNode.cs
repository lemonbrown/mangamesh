using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models; // For AnnounceRequest

namespace MangaMesh.Peer.Core.Node
{

    public class DhtNode : IDhtNode
    {
        public struct ProviderInfo
        {
            public byte[] NodeId;
            public NodeAddress Address;
        }

        private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(15);
        private readonly TimeSpan _reannounceInterval = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _pingInterval = TimeSpan.FromMinutes(5);

        private CancellationTokenSource? _maintenanceToken;

        private IKeyPairService _keypairService;

        private IKeyStore _keyStore;

        public INodeIdentity Identity { get; private set; }
        public ITransport Transport { get; private set; }
        public IDhtStorage Storage { get; private set; }
        public List<KBucket> RoutingTable { get; private set; } = new();
        private bool _running = false;


        private ITrackerClient _tracker;
        private INodeConnectionInfoProvider _connectionInfo;

        public DhtNode(INodeIdentity identity, ITransport transport, IDhtStorage storage, IKeyPairService keyPairService, IKeyStore keyStore, ITrackerClient tracker, INodeConnectionInfoProvider connectionInfo)
        {
            _tracker = tracker;
            _connectionInfo = connectionInfo;
            _keypairService = keyPairService;
            Identity = identity;
            Transport = transport;
            Storage = storage;
            _keyStore = keyStore;
            // Initialize 256 k-buckets for 256-bit node IDs
            for (int i = 0; i < 256; i++)
                RoutingTable.Add(new KBucket());
        }


        /// <summary>
        /// Start the DHT node and maintenance loops.
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskCompletionSource<DhtMessage>> _pendingRequests = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TaskCompletionSource<ContentMessage>> _pendingContentRequests = new();

        /// <summary>
        /// Start the DHT node and maintenance loops.
        /// </summary>
        public void StartWithMaintenance(bool enableBootstrap = true, List<RoutingEntry>? bootstrapNodes = null)
        {
            Start(enableBootstrap, bootstrapNodes);
            _maintenanceToken = new CancellationTokenSource();
            Task.Run(() => MaintenanceLoopAsync(_maintenanceToken.Token));
        }

        /// <summary>
        /// Stop the DHT node and maintenance loops.
        /// </summary>
        public void StopWithMaintenance()
        {
            Stop();
            _maintenanceToken?.Cancel();
        }

        /// <summary>
        /// Periodic maintenance loop:
        /// - Re-announce stored content
        /// - Ping stale nodes
        /// - Refresh k-buckets
        /// </summary>
        private async Task MaintenanceLoopAsync(CancellationToken token)
        {
            var lastReannounce = DateTime.UtcNow;
            var lastPing = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                // ---------------------------
                // Re-announce content
                // ---------------------------
                if (now - lastReannounce > _reannounceInterval)
                {
                    foreach (var content in Storage.GetAllContentHashes())
                    {
                        await StoreAsync(content);
                    }
                    lastReannounce = now;
                }

                // ---------------------------
                // Ping stale nodes
                // ---------------------------
                if (now - lastPing > _pingInterval)
                {
                    foreach (var bucket in RoutingTable)
                    {
                        var staleNodes = bucket.Entries
                            .Where(e => (now - e.LastSeenUtc) > _pingInterval)
                            .ToList();

                        foreach (var node in staleNodes)
                        {
                            await PingAsync(node);
                        }
                    }
                    lastPing = now;
                }

                //Announce to the index
                await AnnounceToIndexAsync();


                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
        }

        private async Task AnnounceToIndexAsync()
        {
            try
            {
                Console.WriteLine("DhtNode: Announcing to index...");
                // Use our Transport Port for DHT announcements

                var (ip, _) = await _connectionInfo.GetConnectionInfoAsync();
                var port = Transport.Port; // Always announce OUR listening port for DHT

                var manifests = Storage.GetAllContentHashes().Select(h => Convert.ToHexString(h).ToLowerInvariant()).ToList();
                Console.WriteLine($"DhtNode: Found {manifests.Count} manifests to announce.");

                var request = new Shared.Models.AnnounceRequest(
                    Convert.ToHexString(Identity.NodeId).ToLowerInvariant(),
                    manifests
                );

                await _tracker.AnnounceAsync(request);
                Console.WriteLine("DhtNode: Announcement successful.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to announce to index: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Start the node.
        /// </summary>
        public void Start(bool enableBootstrap = true, List<RoutingEntry>? overrideBootstrapNodes = null)
        {
            if (_running) return;

            //bootstrap nodes
            var bootstrapNodes = overrideBootstrapNodes ?? new List<RoutingEntry>();

            if (overrideBootstrapNodes == null)
            {
                try
                {
                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "bootstrap_nodes.yml");
                    if (File.Exists(configPath))
                    {
                        var yaml = File.ReadAllText(configPath);
                        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
                            .Build();

                        var nodes = deserializer.Deserialize<List<BootstrapNodeConfig>>(yaml);

                        if (nodes != null)
                        {
                            foreach (var node in nodes)
                            {
                                bootstrapNodes.Add(new RoutingEntry
                                {
                                    NodeId = Convert.FromHexString(node.NodeId),
                                    Address = new NodeAddress(node.Address.Host, node.Address.Port),
                                    LastSeenUtc = DateTime.UtcNow
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load bootstrap nodes: {ex.Message}");
                }

                //ensure keys are available
                var keys = _keyStore.GetAsync().Result;

                if (keys == null)
                    _keypairService.GenerateKeyPairBase64Async().Wait();

                _running = true;

                // Message loop is removed as it is now handled by ProtocolRouter/Handler logic externally

                if (enableBootstrap)
                {
                    Task.Run(async () => await BootstrapAsync(bootstrapNodes));
                }
            }
        }

        public async Task StoreAsync(byte[] contentHash)
        {
            // Find k closest nodes (simplified)
            var closestNodes = FindClosestNodes(contentHash, 20);
            foreach (var node in closestNodes)
            {
                var message = new DhtMessage
                {
                    Type = DhtMessageType.Store,
                    SenderNodeId = Identity.NodeId,
                    Payload = contentHash,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(contentHash),
                    SenderPort = Transport.Port
                };
                await SendDhtMessageAsync(node.Address, message);
            }
            // Also store locally
            Storage.StoreContent(contentHash, Identity.NodeId);
        }

        public async Task<List<byte[]>> FindValueAsync(byte[] contentHash)
        {
            // simplified iterative lookup
            var resultNodes = new List<byte[]>();
            var closestNodes = FindClosestNodes(contentHash, 20);

            foreach (var node in closestNodes)
            {
                var message = new DhtMessage
                {
                    Type = DhtMessageType.FindValue,
                    SenderNodeId = Identity.NodeId,
                    Payload = contentHash,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(contentHash),
                    SenderPort = Transport.Port
                };

                // Wait for response
                var response = await SendDhtMessageAsync(node.Address, message, waitForResponse: true);

                if (response != null)
                {
                    if (response.Type == DhtMessageType.Value)
                    {
                        var providers = JsonSerializer.Deserialize<List<byte[]>>(Encoding.UTF8.GetString(response.Payload));
                        if (providers != null)
                        {
                            resultNodes.AddRange(providers);
                        }
                    }
                    else if (response.Type == DhtMessageType.Nodes)
                    {
                        // Could update closestNodes here for recursive lookup
                    }
                }
            }

            // Include local store if present
            resultNodes.AddRange(Storage.GetNodesForContent(contentHash));

            // Deduplicate
            return resultNodes.Distinct(new ByteArrayComparer()).ToList();
        }

        public async Task<ContentMessage?> SendContentRequestAsync(NodeAddress address, ContentMessage message, TimeSpan timeout)
        {
            message.SenderPort = Transport.Port;

            var tcs = new TaskCompletionSource<ContentMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingContentRequests[message.RequestId] = tcs;

            try
            {
                var json = JsonSerializer.Serialize<ContentMessage>(message);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var payload = new byte[1 + jsonBytes.Length];
                payload[0] = (byte)ProtocolKind.Content;
                Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

                await Transport.SendAsync(address, new ReadOnlyMemory<byte>(payload));

                var timeoutTask = Task.Delay(timeout);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completed == tcs.Task)
                {
                    return await tcs.Task;
                }
                else
                {
                    _pendingContentRequests.TryRemove(message.RequestId, out _);
                    return null;
                }
            }
            catch
            {
                _pendingContentRequests.TryRemove(message.RequestId, out _);
                return null;
            }
        }

        // Internal method to feed content messages from handler
        public void HandleContentMessage(ContentMessage message)
        {
            if (_pendingContentRequests.TryRemove(message.RequestId, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }

        public async Task<List<ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash)
        {
            var providers = new List<ProviderInfo>();
            var closestNodes = FindClosestNodes(contentHash, 20);

            foreach (var node in closestNodes)
            {
                var message = new DhtMessage
                {
                    Type = DhtMessageType.FindValue,
                    SenderNodeId = Identity.NodeId,
                    Payload = contentHash,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(contentHash),
                    SenderPort = Transport.Port
                };

                var response = await SendDhtMessageAsync(node.Address, message, waitForResponse: true);

                if (response != null && response.Type == DhtMessageType.Value)
                {
                    var providerIds = JsonSerializer.Deserialize<List<byte[]>>(Encoding.UTF8.GetString(response.Payload));
                    if (providerIds != null)
                    {
                        foreach (var pid in providerIds)
                        {
                            var address = GetAddressForNode(pid);
                            if (address != null)
                            {
                                providers.Add(new ProviderInfo { NodeId = pid, Address = address });
                            }
                            else if (pid.SequenceEqual(response.SenderNodeId) && !string.IsNullOrEmpty(response.ComputedSenderIp))
                            {
                                // The responder is the provider
                                providers.Add(new ProviderInfo { NodeId = pid, Address = new NodeAddress(response.ComputedSenderIp, response.SenderPort) });
                            }
                        }
                    }
                }
            }

            // Local store check
            var localProviders = Storage.GetNodesForContent(contentHash);
            foreach (var pid in localProviders)
            {
                if (pid.SequenceEqual(Identity.NodeId))
                {
                    // We are the provider
                }
            }

            return providers;
        }

        private async Task HandleFindValueAsync(DhtMessage message)
        {
            var contentHash = message.Payload;
            var nodesWithContent = Storage.GetNodesForContent(contentHash);

            DhtMessage reply;
            if (nodesWithContent.Count > 0)
            {
                reply = new DhtMessage
                {
                    Type = DhtMessageType.Value,
                    SenderNodeId = Identity.NodeId,
                    Payload = SerializeNodeIds(nodesWithContent),
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(SerializeNodeIds(nodesWithContent)),
                    SenderPort = Transport.Port,
                    RequestId = message.RequestId // Echo ID
                };
            }
            else
            {
                // Return closest nodes instead
                var closestNodes = FindClosestNodes(contentHash, 20);
                reply = new DhtMessage
                {
                    Type = DhtMessageType.Nodes,
                    SenderNodeId = Identity.NodeId,
                    Payload = SerializeNodes(closestNodes),
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(SerializeNodes(closestNodes)),
                    SenderPort = Transport.Port,
                    RequestId = message.RequestId // Echo ID
                };
            }

            var senderAddress = GetAddressForNode(message.SenderNodeId);
            if (senderAddress != null)
                await SendDhtMessageAsync(senderAddress, reply);
        }

        public async Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrap = null)
        {
            // simplified iterative lookup
            var closestNodes = new List<RoutingEntry>();

            if (bootstrap != null)
            {
                closestNodes.Add(bootstrap);
            }
            else
            {
                closestNodes.AddRange(FindClosestNodes(nodeId, 20));
            }

            foreach (var node in closestNodes)
            {
                Console.WriteLine($"Looking for node [{node.NodeId}] - [{node.Address.Host}:{node.Address.Port}]");

                var message = new DhtMessage
                {
                    Type = DhtMessageType.FindNode,
                    SenderNodeId = Identity.NodeId,
                    Payload = nodeId,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(nodeId),
                    SenderPort = Transport.Port
                };

                var response = await SendDhtMessageAsync(node.Address, message, waitForResponse: true);

                if (response != null && response.Type == DhtMessageType.Nodes)
                {
                    // Update routing table logic omitted for brevity, but this would parse nodes
                }
            }
            return closestNodes;
        }

        public async Task PingAsync(RoutingEntry node)
        {
            var message = new DhtMessage
            {
                Type = DhtMessageType.Ping,
                SenderNodeId = Identity.NodeId,
                Payload = Array.Empty<byte>(),
                TimestampUtc = DateTime.UtcNow,
                Signature = Identity.Sign(Array.Empty<byte>()),
                SenderPort = Transport.Port
            };
            await SendDhtMessageAsync(node.Address, message);
        }


        /// <summary>
        /// Stop the DHT node
        /// </summary>
        public void Stop()
        {
            _running = false;
        }

        public async Task BootstrapAsync(IEnumerable<RoutingEntry> bootstrapNodes)
        {
            foreach (var bootstrap in bootstrapNodes)
            {
                // Use a random node ID for initial queries
                var randomId = Crypto.RandomNodeId();

                try
                {
                    var foundNodes = await FindNodeAsync(randomId, bootstrap);
                    foreach (var node in foundNodes)
                    {
                        if (node.NodeId != null)
                            UpdateRoutingTable(node.NodeId, node.Address);
                    }

                    if (foundNodes.Count > 0)
                    {
                        break;
                    }
                }
                catch
                {
                    // ignore unreachable bootstrap nodes
                }
            }
        }

        // Made public for ProtocolHandler
        public async Task HandleMessageAsync(DhtMessage message)
        {
            // Update routing table with sender - DO THIS FIRST so we learn about nodes from responses too
            if (!string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
            {
                UpdateRoutingTable(message.SenderNodeId, new NodeAddress(message.ComputedSenderIp, message.SenderPort));
            }
            else
            {
                Console.WriteLine($"Warning: Received message without valid address info. IP: {message.ComputedSenderIp}, Port: {message.SenderPort}");
            }

            // Check if it's a response to a pending request
            if (_pendingRequests.TryRemove(message.RequestId, out var tcs))
            {
                tcs.TrySetResult(message);
                return;
            }

            Console.WriteLine($"Received message [{message.Type}]");

            switch (message.Type)
            {
                case DhtMessageType.Ping:
                    await HandlePingAsync(message);
                    break;
                case DhtMessageType.Pong:
                    break;
                case DhtMessageType.FindNode:
                    await HandleFindNodeAsync(message);
                    break;
                case DhtMessageType.Nodes:
                    break;
                case DhtMessageType.Store:
                    await HandleStoreAsync(message);
                    break;
                case DhtMessageType.FindValue:
                    await HandleFindValueAsync(message);
                    break;
                case DhtMessageType.Value:
                    break;
                default:
                    break;
            }
        }

        // ======================================================
        // Handlers
        // ======================================================

        private async Task HandlePingAsync(DhtMessage message)
        {
            var pong = new DhtMessage
            {
                Type = DhtMessageType.Pong,
                SenderNodeId = Identity.NodeId,
                Payload = Array.Empty<byte>(),
                TimestampUtc = DateTime.UtcNow,
                Signature = Identity.Sign(Array.Empty<byte>()),
                SenderPort = Transport.Port,
                RequestId = message.RequestId // Echo ID
            };

            var senderAddress = GetAddressForNode(message.SenderNodeId);
            if (senderAddress != null)
                await SendDhtMessageAsync(senderAddress, pong);
        }

        private async Task HandleStoreAsync(DhtMessage message)
        {
            // Payload = content hash
            Storage.StoreContent(message.Payload, message.SenderNodeId);
            await Task.CompletedTask; // placeholder
        }

        private async Task HandleFindNodeAsync(DhtMessage message)
        {
            var targetId = message.Payload;
            var closestNodes = FindClosestNodes(targetId, 20);

            var nodesPayload = SerializeNodes(closestNodes);

            var nodesMessage = new DhtMessage
            {
                Type = DhtMessageType.Nodes,
                SenderNodeId = Identity.NodeId,
                Payload = nodesPayload,
                TimestampUtc = DateTime.UtcNow,
                Signature = Identity.Sign(nodesPayload),
                SenderPort = Transport.Port,
                RequestId = message.RequestId // Echo ID
            };

            var senderAddress = GetAddressForNode(message.SenderNodeId);
            if (senderAddress != null)
                await SendDhtMessageAsync(senderAddress, nodesMessage);
        }

        private async Task<DhtMessage?> SendDhtMessageAsync(NodeAddress address, DhtMessage message, bool waitForResponse = false)
        {
            try
            {
                TaskCompletionSource<DhtMessage>? tcs = null;
                if (waitForResponse)
                {
                    tcs = new TaskCompletionSource<DhtMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _pendingRequests[message.RequestId] = tcs;
                }

                var json = JsonSerializer.Serialize(message);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var payload = new byte[1 + jsonBytes.Length];
                payload[0] = (byte)ProtocolKind.Dht;
                Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

                await Transport.SendAsync(address, new ReadOnlyMemory<byte>(payload));

                if (waitForResponse && tcs != null)
                {
                    var timeoutTask = Task.Delay(2000); // 2s timeout
                    var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                    if (completed == tcs.Task)
                    {
                        return await tcs.Task;
                    }
                    else
                    {
                        _pendingRequests.TryRemove(message.RequestId, out _);
                        return null;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex.Message}");
                _pendingRequests.TryRemove(message.RequestId, out _);
                return null;
            }
        }


        // ======================================================
        // Helpers
        // ======================================================

        private void UpdateRoutingTable(byte[] nodeId, NodeAddress address)
        {
            if (nodeId == null || nodeId.Length == 0) return;

            Console.WriteLine($"Adding node [{address.Host}:{address.Port}] to routing table");
            int bucketIndex = GetBucketIndex(nodeId);
            var entry = new RoutingEntry
            {
                NodeId = nodeId,
                Address = address,
                LastSeenUtc = DateTime.UtcNow
            };
            RoutingTable[bucketIndex].AddOrUpdate(entry);
        }

        private int GetBucketIndex(byte[] nodeId)
        {
            // XOR distance first differing bit
            var distance = Crypto.XorDistance(Identity.NodeId, nodeId);
            return (int)Math.Min(255, distance.BitLength() - 1);
        }

        private NodeAddress? GetAddressForNode(byte[] nodeId)
        {
            foreach (var bucket in RoutingTable)
            {
                var entry = bucket.Entries.Find(e => e.NodeId.AsSpan().SequenceEqual(nodeId));
                if (entry != null) return entry.Address;
            }
            return null;
        }

        private byte[] SerializeNodes(List<RoutingEntry> nodes)
        {
            // naive JSON serialization
            var addresses = nodes.ConvertAll(n => new { Host = n.Address.Host, Port = n.Address.Port });
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(addresses);
        }

        private byte[] SerializeNodeIds(List<byte[]> nodeIds)
        {
            return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(nodeIds);
        }

        private List<RoutingEntry> FindClosestNodes(byte[] targetNodeId, int count)
        {
            // naive linear scan; XOR distance
            var allEntries = new List<RoutingEntry>();
            foreach (var bucket in RoutingTable)
                allEntries.AddRange(bucket.Entries);

            allEntries.Sort((a, b) =>
            {
                var distA = Crypto.XorDistance(a.NodeId, targetNodeId);
                var distB = Crypto.XorDistance(b.NodeId, targetNodeId);
                return distA.CompareTo(distB);
            });

            return allEntries.GetRange(0, Math.Min(count, allEntries.Count));
        }

        class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == null || y == null) return x == y;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj)
            {
                return BitConverter.ToInt32(obj, 0); // simplified
            }
        }
    }

    public class BootstrapNodeConfig
    {
        public string NodeId { get; set; } = string.Empty;
        public BootstrapAddressConfig Address { get; set; } = new();
    }

    public class BootstrapAddressConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
    }
}

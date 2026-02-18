using NSec.Cryptography;
using System.Text;
using System.Text.Json;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Logging;

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

        private readonly IKeyPairService _keypairService;
        private readonly IKeyStore _keyStore;
        private readonly IRoutingTable _routingTable;
        private readonly IBootstrapNodeProvider _bootstrapNodeProvider;
        private readonly IDhtRequestTracker _requestTracker;
        private readonly INodeAnnouncer _tracker;
        private readonly INodeConnectionInfoProvider _connectionInfo;
        private readonly ILogger<DhtNode> _logger;

        private bool _running = false;

        public INodeIdentity Identity { get; private set; }
        public ITransport Transport { get; private set; }
        public IDhtStorage Storage { get; private set; }
        public IRoutingTable RoutingTable => _routingTable;

        public DhtNode(
            INodeIdentity identity,
            ITransport transport,
            IDhtStorage storage,
            IRoutingTable routingTable,
            IBootstrapNodeProvider bootstrapNodeProvider,
            IDhtRequestTracker requestTracker,
            IKeyPairService keyPairService,
            IKeyStore keyStore,
            INodeAnnouncer tracker,
            INodeConnectionInfoProvider connectionInfo,
            ILogger<DhtNode> logger)
        {
            Identity = identity;
            Transport = transport;
            Storage = storage;
            _routingTable = routingTable;
            _bootstrapNodeProvider = bootstrapNodeProvider;
            _requestTracker = requestTracker;
            _keypairService = keyPairService;
            _keyStore = keyStore;
            _tracker = tracker;
            _connectionInfo = connectionInfo;
            _logger = logger;
        }

        private readonly List<RoutingEntry> _knownBootstrapNodes = new();

        public void StartWithMaintenance(bool enableBootstrap = true, List<RoutingEntry>? bootstrapNodes = null)
        {
            Start(enableBootstrap, bootstrapNodes);
            _maintenanceToken = new CancellationTokenSource();
            Task.Run(() => MaintenanceLoopAsync(_maintenanceToken.Token));
        }

        public void StopWithMaintenance()
        {
            Stop();
            _maintenanceToken?.Cancel();
        }

        private async Task MaintenanceLoopAsync(CancellationToken token)
        {
            var lastReannounce = DateTime.UtcNow;
            var lastPing = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                if (now - lastReannounce > _reannounceInterval)
                {
                    foreach (var content in Storage.GetAllContentHashes())
                        await StoreAsync(content);
                    lastReannounce = now;
                }

                if (now - lastPing > _pingInterval)
                {
                    foreach (var entry in _routingTable.GetAll())
                    {
                        if ((now - entry.LastSeenUtc) > _pingInterval)
                            await PingAsync(entry);
                    }
                    lastPing = now;
                }

                await AnnounceToIndexAsync();
                await Task.Delay(TimeSpan.FromSeconds(10), token);
            }
        }

        private async Task AnnounceToIndexAsync()
        {
            try
            {
                _logger.LogDebug("DhtNode: Announcing to index...");
                var (ip, _) = await _connectionInfo.GetConnectionInfoAsync();
                var port = Transport.Port;

                var manifests = Storage.GetAllContentHashes()
                    .Select(h => Convert.ToHexString(h).ToLowerInvariant())
                    .ToList();
                _logger.LogDebug("DhtNode: Found {Count} manifests to announce.", manifests.Count);

                var request = new Shared.Models.AnnounceRequest(
                    Convert.ToHexString(Identity.NodeId).ToLowerInvariant(),
                    manifests);

                await _tracker.AnnounceAsync(request);
                _logger.LogDebug("DhtNode: Announcement successful.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to announce to index");
            }
        }

        public void Start(bool enableBootstrap = true, List<RoutingEntry>? overrideBootstrapNodes = null)
        {
            if (_running) return;

            var keys = _keyStore.GetAsync().Result;
            if (keys == null)
                _keypairService.GenerateKeyPairBase64Async().Wait();

            _running = true;

            if (enableBootstrap)
            {
                Task.Run(async () =>
                {
                    List<RoutingEntry> bootstrapNodes;
                    if (overrideBootstrapNodes != null)
                    {
                        bootstrapNodes = overrideBootstrapNodes;
                    }
                    else
                    {
                        var provided = await _bootstrapNodeProvider.GetBootstrapNodesAsync();
                        bootstrapNodes = provided.ToList();
                    }

                    lock (_knownBootstrapNodes)
                    {
                        _knownBootstrapNodes.Clear();
                        _knownBootstrapNodes.AddRange(bootstrapNodes);
                    }

                    await BootstrapAsync(bootstrapNodes);
                });
            }
        }

        public async Task StoreAsync(byte[] contentHash)
        {
            var closestNodes = _routingTable.FindClosest(contentHash, 20);
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
            Storage.StoreContent(contentHash, Identity.NodeId);
        }

        public async Task<List<byte[]>> FindValueAsync(byte[] contentHash)
        {
            var resultNodes = new List<byte[]>();
            var visited = new HashSet<string>();
            var nodesToQuery = new List<RoutingEntry>();

            nodesToQuery.AddRange(_routingTable.FindClosest(contentHash, 20));

            if (nodesToQuery.Count == 0)
            {
                lock (_knownBootstrapNodes)
                {
                    nodesToQuery.AddRange(_knownBootstrapNodes);
                }
            }

            nodesToQuery.Sort((a, b) =>
            {
                if (a.NodeId == null || a.NodeId.Length == 0) return 1;
                if (b.NodeId == null || b.NodeId.Length == 0) return -1;
                return Crypto.XorDistance(a.NodeId, contentHash).CompareTo(Crypto.XorDistance(b.NodeId, contentHash));
            });

            int queriedCount = 0;
            const int MaxQueries = 20;

            while (nodesToQuery.Count > 0 && queriedCount < MaxQueries)
            {
                var candidate = nodesToQuery.FirstOrDefault(n => !visited.Contains($"{n.Address.Host}:{n.Address.Port}"));
                if (candidate == null) break;

                visited.Add($"{candidate.Address.Host}:{candidate.Address.Port}");
                nodesToQuery.Remove(candidate);
                queriedCount++;

                var message = new DhtMessage
                {
                    Type = DhtMessageType.FindValue,
                    SenderNodeId = Identity.NodeId,
                    Payload = contentHash,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(contentHash),
                    SenderPort = Transport.Port
                };

                var response = await SendDhtMessageAsync(candidate.Address, message, waitForResponse: true);

                if (response != null)
                {
                    if ((candidate.NodeId == null || candidate.NodeId.Length == 0) && response.SenderNodeId?.Length > 0)
                    {
                        candidate.NodeId = response.SenderNodeId;
                        _routingTable.AddOrUpdate(new RoutingEntry
                        {
                            NodeId = response.SenderNodeId,
                            Address = candidate.Address,
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }

                    if (response.Type == DhtMessageType.Value)
                    {
                        var providers = JsonSerializer.Deserialize<List<byte[]>>(Encoding.UTF8.GetString(response.Payload));
                        if (providers != null)
                            resultNodes.AddRange(providers);

                        // Continue searching? Kademlia usually stops if k providers found.
                        if (resultNodes.Count > 0) break;
                    }
                    else if (response.Type == DhtMessageType.Nodes)
                    {
                        try
                        {
                            var nodesJson = Encoding.UTF8.GetString(response.Payload);
                            var discovered = JsonSerializer.Deserialize<List<NodeEntry>>(nodesJson,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (discovered != null)
                            {
                                foreach (var d in discovered)
                                {
                                    if (!string.IsNullOrEmpty(d.NodeId) && d.Port > 0)
                                    {
                                        var dId = Convert.FromHexString(d.NodeId);
                                        var newEntry = new RoutingEntry { NodeId = dId, Address = new NodeAddress(d.Host, d.Port), LastSeenUtc = DateTime.UtcNow };
                                        _routingTable.AddOrUpdate(newEntry);
                                        if (!visited.Contains($"{d.Host}:{d.Port}")) nodesToQuery.Add(newEntry);
                                    }
                                }
                                nodesToQuery.Sort((a, b) =>
                                {
                                    if (a.NodeId == null || a.NodeId.Length == 0) return 1;
                                    if (b.NodeId == null || b.NodeId.Length == 0) return -1;
                                    return Crypto.XorDistance(a.NodeId, contentHash).CompareTo(Crypto.XorDistance(b.NodeId, contentHash));
                                });
                            }
                        }
                        catch { }
                    }
                }
            }

            resultNodes.AddRange(Storage.GetNodesForContent(contentHash));
            return resultNodes.Distinct(new ByteArrayComparer()).ToList();
        }

        public async Task<ContentMessage?> SendContentRequestAsync(NodeAddress address, ContentMessage message, TimeSpan timeout)
        {
            message.SenderPort = Transport.Port;

            var tcs = new TaskCompletionSource<ContentMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requestTracker.RegisterContent(message.RequestId, tcs);

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
                    return await tcs.Task;

                _requestTracker.CancelContent(message.RequestId);
                return null;
            }
            catch
            {
                _requestTracker.CancelContent(message.RequestId);
                return null;
            }
        }

        public void HandleContentMessage(ContentMessage message)
        {
            _requestTracker.TryCompleteContent(message.RequestId, message);
        }

        public async Task<List<ProviderInfo>> FindValueWithAddressAsync(byte[] contentHash)
        {
            var providers = new List<ProviderInfo>();
            var visited = new HashSet<string>();
            var nodesToQuery = new List<RoutingEntry>();

            // Initial set: Closest nodes from routing table
            nodesToQuery.AddRange(_routingTable.FindClosest(contentHash, 20));

            // Fallback: If routing table is empty (or returns nothing), use bootstrap nodes
            if (nodesToQuery.Count == 0)
            {
                lock (_knownBootstrapNodes)
                {
                    nodesToQuery.AddRange(_knownBootstrapNodes);
                }
            }

            // Sort by distance to target
            nodesToQuery.Sort((a, b) =>
            {
                // Handle missing IDs by pushing them to the end or treating as far
                if (a.NodeId == null || a.NodeId.Length == 0) return 1;
                if (b.NodeId == null || b.NodeId.Length == 0) return -1;
                return Crypto.XorDistance(a.NodeId, contentHash).CompareTo(Crypto.XorDistance(b.NodeId, contentHash));
            });

            int queriedCount = 0;
            const int MaxQueries = 20;

            while (nodesToQuery.Count > 0 && queriedCount < MaxQueries)
            {
                // Pick best candidate that hasn't been visited
                var candidate = nodesToQuery.FirstOrDefault(n => !visited.Contains($"{n.Address.Host}:{n.Address.Port}"));
                if (candidate == null) break;

                visited.Add($"{candidate.Address.Host}:{candidate.Address.Port}");
                nodesToQuery.Remove(candidate);
                queriedCount++;

                var message = new DhtMessage
                {
                    Type = DhtMessageType.FindValue,
                    SenderNodeId = Identity.NodeId,
                    Payload = contentHash,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(contentHash),
                    SenderPort = Transport.Port
                };

                var response = await SendDhtMessageAsync(candidate.Address, message, waitForResponse: true);

                if (response != null)
                {
                    // Opportunistic Update: If we queried a node with unknown ID and it responded, update it
                    if ((candidate.NodeId == null || candidate.NodeId.Length == 0) && response.SenderNodeId?.Length > 0)
                    {
                        candidate.NodeId = response.SenderNodeId;
                        _routingTable.AddOrUpdate(new RoutingEntry
                        {
                            NodeId = response.SenderNodeId,
                            Address = candidate.Address,
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }

                    if (response.Type == DhtMessageType.Value)
                    {
                        var providerIds = JsonSerializer.Deserialize<List<byte[]>>(Encoding.UTF8.GetString(response.Payload));
                        if (providerIds != null)
                        {
                            foreach (var pid in providerIds)
                            {
                                var address = _routingTable.GetAddressForNode(pid);
                                if (address != null)
                                {
                                    providers.Add(new ProviderInfo { NodeId = pid, Address = address });
                                }
                                else if (pid.SequenceEqual(response.SenderNodeId) && !string.IsNullOrEmpty(response.ComputedSenderIp))
                                {
                                    providers.Add(new ProviderInfo { NodeId = pid, Address = new NodeAddress(response.ComputedSenderIp, response.SenderPort) });
                                }
                            }
                        }
                        // Found value! We can return or keep searching for more redundancy. 
                        // For Peer Discovery, usually finding one set of providers is enough.
                        if (providers.Count > 0) return providers;
                    }
                    else if (response.Type == DhtMessageType.Nodes)
                    {
                        // Iterative Step: Add returned nodes to candidates
                        try
                        {
                            var nodesJson = Encoding.UTF8.GetString(response.Payload);
                            var discovered = JsonSerializer.Deserialize<List<NodeEntry>>(nodesJson,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (discovered != null)
                            {
                                foreach (var d in discovered)
                                {
                                    if (!string.IsNullOrEmpty(d.NodeId) && d.Port > 0)
                                    {
                                        var dId = Convert.FromHexString(d.NodeId);
                                        var newEntry = new RoutingEntry
                                        {
                                            NodeId = dId,
                                            Address = new NodeAddress(d.Host, d.Port), // Assuming Host is IP or resolvable
                                            LastSeenUtc = DateTime.UtcNow
                                        };

                                        // Add to routing table if appropriate
                                        _routingTable.AddOrUpdate(newEntry);

                                        // Add to query list if not visited
                                        if (!visited.Contains($"{d.Host}:{d.Port}"))
                                        {
                                            nodesToQuery.Add(newEntry);
                                        }
                                    }
                                }
                                // Re-sort to prioritize closest
                                nodesToQuery.Sort((a, b) =>
                                {
                                    if (a.NodeId == null || a.NodeId.Length == 0) return 1;
                                    if (b.NodeId == null || b.NodeId.Length == 0) return -1;
                                    return Crypto.XorDistance(a.NodeId, contentHash).CompareTo(Crypto.XorDistance(b.NodeId, contentHash));
                                });
                            }
                        }
                        catch(Exception exception)
                        {
                            // ignore parse errors
                        }
                    }
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
                var payload = SerializeNodeIds(nodesWithContent);
                reply = new DhtMessage
                {
                    Type = DhtMessageType.Value,
                    SenderNodeId = Identity.NodeId,
                    Payload = payload,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(payload),
                    SenderPort = Transport.Port,
                    RequestId = message.RequestId
                };
            }
            else
            {
                var closestNodes = _routingTable.FindClosest(contentHash, 20);
                var payload = SerializeNodes(closestNodes);
                reply = new DhtMessage
                {
                    Type = DhtMessageType.Nodes,
                    SenderNodeId = Identity.NodeId,
                    Payload = payload,
                    TimestampUtc = DateTime.UtcNow,
                    Signature = Identity.Sign(payload),
                    SenderPort = Transport.Port,
                    RequestId = message.RequestId
                };
            }

            var senderAddress = _routingTable.GetAddressForNode(message.SenderNodeId);
            if (senderAddress == null && !string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
            {
                senderAddress = new NodeAddress(message.ComputedSenderIp, message.SenderPort);
            }

            if (senderAddress != null)
                await SendDhtMessageAsync(senderAddress, reply);
        }

        public async Task<List<RoutingEntry>> FindNodeAsync(byte[] nodeId, RoutingEntry? bootstrap = null)
        {
            var closestNodes = new List<RoutingEntry>();
            var queriedNodes = new HashSet<string>();

            if (bootstrap != null)
                closestNodes.Add(bootstrap);
            else
                closestNodes.AddRange(_routingTable.FindClosest(nodeId, 20));

            for (int i = 0; i < closestNodes.Count; i++)
            {
                var node = closestNodes[i];
                var nodeKey = $"{node.Address.Host}:{node.Address.Port}";

                if (queriedNodes.Contains(nodeKey)) continue;
                queriedNodes.Add(nodeKey);

                _logger.LogDebug("Looking for node [{NodeId}] - [{Host}:{Port}]",
                    Convert.ToHexString(node.NodeId ?? Array.Empty<byte>()),
                    node.Address.Host, node.Address.Port);

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

                if (response != null)
                {
                    _logger.LogDebug("Received response from {IP}:{Port}. SenderID len: {Len}",
                        response.ComputedSenderIp, response.SenderPort, response.SenderNodeId?.Length ?? 0);

                    if ((node.NodeId == null || node.NodeId.Length == 0) && response.SenderNodeId?.Length > 0)
                    {
                        _logger.LogDebug("Updating NodeID for bootstrap node to {NodeId}",
                            Convert.ToHexString(response.SenderNodeId));
                        node.NodeId = response.SenderNodeId;
                        _routingTable.AddOrUpdate(new RoutingEntry
                        {
                            NodeId = node.NodeId,
                            Address = node.Address,
                            LastSeenUtc = DateTime.UtcNow
                        });
                    }

                    if (response.Type == DhtMessageType.Nodes)
                    {
                        try
                        {
                            var nodesJson = Encoding.UTF8.GetString(response.Payload);
                            var discovered = JsonSerializer.Deserialize<List<NodeEntry>>(nodesJson,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                            if (discovered != null)
                            {
                                foreach (var d in discovered)
                                {
                                    if (!string.IsNullOrEmpty(d.NodeId) && !string.IsNullOrEmpty(d.Host) && d.Port > 0)
                                    {
                                        var discoveredId = Convert.FromHexString(d.NodeId);
                                        var entry = new RoutingEntry
                                        {
                                            NodeId = discoveredId,
                                            Address = new NodeAddress(d.Host, d.Port),
                                            LastSeenUtc = DateTime.UtcNow
                                        };
                                        _routingTable.AddOrUpdate(entry);

                                        if (!closestNodes.Any(n => n.NodeId != null && n.NodeId.SequenceEqual(discoveredId)))
                                            closestNodes.Add(entry);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse FindNode response");
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("Timeout or no response from {Host}:{Port}", node.Address.Host, node.Address.Port);
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

        public void Stop()
        {
            _running = false;
        }

        public async Task BootstrapAsync(IEnumerable<RoutingEntry> bootstrapNodes)
        {
            foreach (var bootstrap in bootstrapNodes)
            {
                var randomId = Crypto.RandomNodeId();
                try
                {
                    var foundNodes = await FindNodeAsync(randomId, bootstrap);
                    bool success = false;
                    foreach (var node in foundNodes)
                    {
                        if (node.NodeId?.Length > 0)
                        {
                            _routingTable.AddOrUpdate(new RoutingEntry
                            {
                                NodeId = node.NodeId,
                                Address = node.Address,
                                LastSeenUtc = DateTime.UtcNow
                            });
                            success = true;
                        }
                    }
                    if (success) break;
                }
                catch
                {
                    // ignore unreachable bootstrap nodes
                }
            }
        }

        public async Task HandleMessageAsync(DhtMessage message)
        {
            if (!string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
            {
                _routingTable.AddOrUpdate(new RoutingEntry
                {
                    NodeId = message.SenderNodeId,
                    Address = new NodeAddress(message.ComputedSenderIp, message.SenderPort),
                    LastSeenUtc = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogDebug("Warning: Received message without valid address info. IP: {IP}, Port: {Port}",
                    message.ComputedSenderIp, message.SenderPort);
            }

            if (_requestTracker.TryComplete(message.RequestId, message))
                return;

            _logger.LogDebug("Received message [{Type}]", message.Type);

            switch (message.Type)
            {
                case DhtMessageType.Ping:
                    await HandlePingAsync(message);
                    break;
                case DhtMessageType.FindNode:
                    await HandleFindNodeAsync(message);
                    break;
                case DhtMessageType.Store:
                    await HandleStoreAsync(message);
                    break;
                case DhtMessageType.FindValue:
                    await HandleFindValueAsync(message);
                    break;
                default:
                    break;
            }
        }

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
                RequestId = message.RequestId
            };
            var senderAddress = _routingTable.GetAddressForNode(message.SenderNodeId);
            if (senderAddress == null && !string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
            {
                senderAddress = new NodeAddress(message.ComputedSenderIp, message.SenderPort);
            }

            if (senderAddress != null)
                await SendDhtMessageAsync(senderAddress, pong);
        }

        private Task HandleStoreAsync(DhtMessage message)
        {
            Storage.StoreContent(message.Payload, message.SenderNodeId);
            return Task.CompletedTask;
        }

        private async Task HandleFindNodeAsync(DhtMessage message)
        {
            var targetId = message.Payload;
            var closestNodes = _routingTable.FindClosest(targetId, 20);
            var nodesPayload = SerializeNodes(closestNodes);

            var reply = new DhtMessage
            {
                Type = DhtMessageType.Nodes,
                SenderNodeId = Identity.NodeId,
                Payload = nodesPayload,
                TimestampUtc = DateTime.UtcNow,
                Signature = Identity.Sign(nodesPayload),
                SenderPort = Transport.Port,
                RequestId = message.RequestId
            };

            var senderAddress = _routingTable.GetAddressForNode(message.SenderNodeId);
            if (senderAddress == null && !string.IsNullOrEmpty(message.ComputedSenderIp) && message.SenderPort > 0)
            {
                senderAddress = new NodeAddress(message.ComputedSenderIp, message.SenderPort);
            }

            if (senderAddress != null)
                await SendDhtMessageAsync(senderAddress, reply);
        }

        private async Task<DhtMessage?> SendDhtMessageAsync(NodeAddress address, DhtMessage message, bool waitForResponse = false)
        {
            try
            {
                TaskCompletionSource<DhtMessage>? tcs = null;
                if (waitForResponse)
                {
                    tcs = new TaskCompletionSource<DhtMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _requestTracker.Register(message.RequestId, tcs);
                }

                var json = JsonSerializer.Serialize(message);
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var payload = new byte[1 + jsonBytes.Length];
                payload[0] = (byte)ProtocolKind.Dht;
                Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

                await Transport.SendAsync(address, new ReadOnlyMemory<byte>(payload));

                if (waitForResponse && tcs != null)
                {
                    var timeoutTask = Task.Delay(2000);
                    var completed = await Task.WhenAny(tcs.Task, timeoutTask);
                    if (completed == tcs.Task)
                        return await tcs.Task;

                    _requestTracker.Cancel(message.RequestId);
                    return null;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send DHT message");
                _requestTracker.Cancel(message.RequestId);
                return null;
            }
        }

        private byte[] SerializeNodes(IReadOnlyList<RoutingEntry> nodes)
        {
            var addresses = nodes.Select(n => new
            {
                Host = n.Address.Host,
                Port = n.Address.Port,
                NodeId = Convert.ToHexString(n.NodeId)
            }).ToList();
            return JsonSerializer.SerializeToUtf8Bytes(addresses);
        }

        private byte[] SerializeNodeIds(List<byte[]> nodeIds)
            => JsonSerializer.SerializeToUtf8Bytes(nodeIds);

        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y)
            {
                if (x == null || y == null) return x == y;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj) => BitConverter.ToInt32(obj, 0);
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

    public class NodeEntry
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string NodeId { get; set; } = string.Empty;
    }
}

using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MangaMesh.Peer.Tests.Helpers
{
    /// <summary>Shared factory for creating test DHT nodes without touching the filesystem.</summary>
    internal static class TestNodeFactory
    {
        public static (DhtNode node, TcpTransport transport) CreateNode(int port)
        {
            var storage = new InMemoryDhtStorage();
            var keyStore = new InMemoryKeyStore();
            var keyPairService = new KeyPairService(keyStore, NullLogger<KeyPairService>.Instance);
            keyPairService.GenerateKeyPairBase64Async().Wait();

            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var identity = new NodeIdentity(keyPairService, mockConfig.Object, keyStore);
            var transport = new TcpTransport(port);

            var mockTracker = new Mock<MangaMesh.Peer.Core.Tracker.ITrackerClient>();
            var connectionInfo = new ConsoleNodeConnectionInfoProvider();
            var routingTable = new KBucketRoutingTable(identity.NodeId);
            var requestTracker = new DhtRequestTracker();
            var bootstrapProvider = new StaticBootstrapNodeProvider(Array.Empty<RoutingEntry>());

            var node = new DhtNode(
                identity, transport, storage,
                routingTable, bootstrapProvider, requestTracker,
                keyPairService, keyStore,
                mockTracker.Object, connectionInfo,
                NullLogger<DhtNode>.Instance);

            return (node, transport);
        }
    }

    /// <summary>In-memory key store for tests — avoids filesystem access.</summary>
    internal sealed class InMemoryKeyStore : IKeyStore
    {
        private PublicPrivateKeyPair? _pair;

        public Task<PublicPrivateKeyPair?> GetAsync() => Task.FromResult(_pair);

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

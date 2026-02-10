
using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Keys;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Tests
{
    [TestClass]
    public class DhtManifestTests
    {
        private List<DhtNode> _nodes = new List<DhtNode>();

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var node in _nodes)
            {
                node.StopWithMaintenance();
            }
            _nodes.Clear();
        }

        [TestMethod]
        public async Task TestManifestAnnouncement()
        {
            // 1. Start Node A and Node B
            int portA = 3000;
            int portB = 3001;

            var nodeA = CreateNode(portA);
            var nodeB = CreateNode(portB);

            _nodes.Add(nodeA);
            _nodes.Add(nodeB);

            // 2. Bootstrap A -> B
            var entry = new RoutingEntry
            {
                Address = new NodeAddress("127.0.0.1", portB)
            };
            await nodeA.BootstrapAsync(new[] { entry });
            await Task.Delay(1000); // Wait for bootstrap

            // 3. Create manifest key
            // "mm:chapter:{seriesId}}:1107:en" -> let's use 12345 for seriesId
            string keyString = "mm:chapter:12345:1107:en";
            byte[] manifestKey = Crypto.Sha256(System.Text.Encoding.UTF8.GetBytes(keyString));

            // 4. Node A announces (stores) the key
            await nodeA.StoreAsync(manifestKey);

            // 5. Wait for propagation
            await Task.Delay(2000);

            // 6. Assertions

            // Assert Node B has the key provider
            var nodesWithContent = nodeB.Storage.GetNodesForContent(manifestKey);
            bool bKnowsAHasIt = nodesWithContent.Any(id => id.AsSpan().SequenceEqual(nodeA.Identity.NodeId));
            Assert.IsTrue(bKnowsAHasIt, "Node B should know that Node A has the manifest.");

            // Assert Node B's routing table contains Node A
            bool routingTableHasA = false;
            foreach (var bucket in nodeB.RoutingTable)
            {
                if (bucket.Entries.Any(e => e.NodeId.AsSpan().SequenceEqual(nodeA.Identity.NodeId)))
                {
                    routingTableHasA = true;
                    break;
                }
            }
            Assert.IsTrue(routingTableHasA, "Node B's routing table should contain Node A.");
        }

        private DhtNode CreateNode(int port)
        {
            var storage = new InMemoryDhtStorage();
            var keyStore = new InMemoryKeyStore();
            var keyPairService = new KeyPairService(keyStore);

            // Generate keys now so we don't have async issues in constructor (though logic handles it)
            keyPairService.GenerateKeyPairBase64Async().Wait();

            var identity = new NodeIdentity(keyPairService);
            var transport = new TcpTransport(port);

            var mockTracker = new Moq.Mock<MangaMesh.Peer.Core.Tracker.ITrackerClient>();
            var connectionInfo = new ConsoleNodeConnectionInfoProvider();

            var node = new DhtNode(identity, transport, storage, keyPairService, keyStore, mockTracker.Object, connectionInfo);

            var router = new ProtocolRouter();
            var handler = new DhtProtocolHandler(node);
            router.Register(handler);
            transport.OnMessage += router.RouteAsync;

            node.StartWithMaintenance(enableBootstrap: false);
            return node;
        }

        private class InMemoryKeyStore : IKeyStore
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
}

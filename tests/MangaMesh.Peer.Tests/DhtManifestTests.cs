
using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Tests.Helpers;
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
            bool routingTableHasA = nodeB.RoutingTable.GetAll()
                .Any(e => e.NodeId.AsSpan().SequenceEqual(nodeA.Identity.NodeId));
            Assert.IsTrue(routingTableHasA, "Node B's routing table should contain Node A.");
        }

        private DhtNode CreateNode(int port)
        {
            var (node, transport) = TestNodeFactory.CreateNode(port);

            var router = new ProtocolRouter();
            var handler = new DhtProtocolHandler { DhtNode = node };
            router.Register(handler);
            transport.OnMessage += router.RouteAsync;

            node.StartWithMaintenance(enableBootstrap: false);
            return node;
        }
    }
}

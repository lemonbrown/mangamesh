using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;
using MangaMesh.Peer.Tests.Helpers;

namespace MangaMesh.Peer.Tests.Dht;

/// <summary>
/// Verifies BootstrapAsync behaviour using real DhtNodes over loopback TCP.
/// Unlike existing tests that use bootstrap only as setup plumbing,
/// these tests explicitly assert the bootstrap outcome.
/// </summary>
[TestClass]
public class DhtBootstrapTests
{
    private readonly List<DhtNode> _nodes = new();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var node in _nodes)
            try { node.StopWithMaintenance(); } catch { }
        _nodes.Clear();
    }

    /// <summary>
    /// Creates a node, wires the ProtocolRouter (required for message handling),
    /// starts it, and registers it for cleanup.
    /// </summary>
    private DhtNode CreateAndStart(int port)
    {
        var (node, transport) = TestNodeFactory.CreateNode(port);

        var router  = new ProtocolRouter();
        var handler = new DhtProtocolHandler { DhtNode = node };
        router.Register(handler);
        transport.OnMessage += router.RouteAsync;

        node.StartWithMaintenance(enableBootstrap: false);
        _nodes.Add(node);
        return node;
    }

    private static RoutingEntry EntryFor(int port) => new()
    {
        Address = new NodeAddress("127.0.0.1", port)
    };

    // ── Happy path ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task BootstrapAsync_SinglePeer_PopulatesRoutingTable()
    {
        var nodeA = CreateAndStart(4200);
        var nodeB = CreateAndStart(4201);

        await nodeB.BootstrapAsync(new[] { EntryFor(4200) });

        var all = nodeB.RoutingTable.GetAll();
        Assert.IsTrue(all.Count > 0,
            "Routing table should be non-empty after a successful bootstrap");
    }

    [TestMethod]
    public async Task BootstrapAsync_SinglePeer_BootstrapNodeLearnsAboutUs()
    {
        var nodeA = CreateAndStart(4202);
        var nodeB = CreateAndStart(4203);

        await nodeB.BootstrapAsync(new[] { EntryFor(4202) });
        await Task.Delay(300); // brief delay for nodeA to process the inbound message

        var all = nodeA.RoutingTable.GetAll();
        Assert.IsTrue(all.Count > 0,
            "Bootstrap node should have recorded the connecting peer in its routing table");
    }

    // ── Failure isolation ────────────────────────────────────────────────────

    [TestMethod]
    public async Task BootstrapAsync_UnreachablePeer_CompletesWithinTimeout()
    {
        var nodeA = CreateAndStart(4204);

        var start = DateTime.UtcNow;
        await nodeA.BootstrapAsync(new[] { EntryFor(4299) }); // no listener
        var elapsed = DateTime.UtcNow - start;

        Assert.IsTrue(elapsed < TimeSpan.FromSeconds(10),
            $"Bootstrap to unreachable peer took too long: {elapsed.TotalSeconds:F1}s");
    }

    [TestMethod]
    public async Task BootstrapAsync_UnreachablePeer_RoutingTableRemainsEmpty()
    {
        var nodeA = CreateAndStart(4205);

        await nodeA.BootstrapAsync(new[] { EntryFor(4298) });

        Assert.AreEqual(0, nodeA.RoutingTable.GetAll().Count,
            "Unreachable peer must not be added to the routing table");
    }

    // ── Multiple peers ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task BootstrapAsync_MultiplePeers_LivePeersAdded()
    {
        var nodeA = CreateAndStart(4206);
        var nodeB = CreateAndStart(4207);
        var nodeC = CreateAndStart(4208);

        await nodeC.BootstrapAsync(new[]
        {
            EntryFor(4206),
            EntryFor(4207)
        });

        var all = nodeC.RoutingTable.GetAll();
        Assert.IsTrue(all.Count > 0,
            "At least one live peer should appear in the routing table");
    }

    [TestMethod]
    public async Task BootstrapAsync_MixOfLiveAndDead_DoesNotHang()
    {
        var nodeA = CreateAndStart(4209);
        var nodeB = CreateAndStart(4210);

        var start = DateTime.UtcNow;
        await nodeB.BootstrapAsync(new[]
        {
            EntryFor(4297), // unreachable
            EntryFor(4209)  // live — note: BootstrapAsync stops after first success
        });
        var elapsed = DateTime.UtcNow - start;

        Assert.IsTrue(elapsed < TimeSpan.FromSeconds(15),
            $"Bootstrap with mixed peers took too long: {elapsed.TotalSeconds:F1}s");
    }

    // ── Empty bootstrap list ──────────────────────────────────────────────────

    [TestMethod]
    public async Task BootstrapAsync_EmptyList_DoesNotThrow()
    {
        var nodeA = CreateAndStart(4211);
        await nodeA.BootstrapAsync(Array.Empty<RoutingEntry>());
        Assert.AreEqual(0, nodeA.RoutingTable.GetAll().Count);
    }
}

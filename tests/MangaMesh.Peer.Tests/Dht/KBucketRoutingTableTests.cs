using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Tests.Dht;

[TestClass]
public class KBucketRoutingTableTests
{
    private static byte[] NodeId(byte seed) => Enumerable.Repeat(seed, 32).ToArray();
    private static NodeAddress Addr(int port) => new("127.0.0.1", port);

    private static RoutingEntry Entry(byte seed, int port = 9000) => new()
    {
        NodeId      = NodeId(seed),
        Address     = Addr(port),
        LastSeenUtc = DateTime.UtcNow
    };

    // Use a fixed local node ID that won't collide with test entries
    private static KBucketRoutingTable CreateTable()
        => new(NodeId(0xFF));

    // ── AddOrUpdate / GetAll ─────────────────────────────────────────────────

    [TestMethod]
    public void AddOrUpdate_NewNode_AppearsInGetAll()
    {
        var table = CreateTable();
        var entry = Entry(0x01);

        table.AddOrUpdate(entry);
        var all = table.GetAll();

        Assert.IsTrue(all.Any(e => e.NodeId.SequenceEqual(entry.NodeId)));
    }

    [TestMethod]
    public void AddOrUpdate_EmptyNodeId_Ignored()
    {
        var table = CreateTable();
        table.AddOrUpdate(new RoutingEntry { NodeId = Array.Empty<byte>(), Address = Addr(1) });

        Assert.AreEqual(0, table.GetAll().Count);
    }

    [TestMethod]
    public void AddOrUpdate_SameNodeId_UpdatesAddress()
    {
        var table    = CreateTable();
        var original = Entry(0x02, 8001);
        table.AddOrUpdate(original);

        var updated = new RoutingEntry
        {
            NodeId      = NodeId(0x02),
            Address     = Addr(8002),
            LastSeenUtc = DateTime.UtcNow
        };
        table.AddOrUpdate(updated);

        var all = table.GetAll();
        Assert.AreEqual(1, all.Count(e => e.NodeId.SequenceEqual(NodeId(0x02))));
        Assert.AreEqual(8002, all.First(e => e.NodeId.SequenceEqual(NodeId(0x02))).Address.Port);
    }

    // ── FindClosest ──────────────────────────────────────────────────────────

    [TestMethod]
    public void FindClosest_ReturnsAtMostK()
    {
        var table = CreateTable();
        for (byte i = 0x01; i <= 30; i++)
            table.AddOrUpdate(Entry(i, 8000 + i));

        var closest = table.FindClosest(NodeId(0x10), k: 5);

        Assert.IsTrue(closest.Count <= 5);
    }

    [TestMethod]
    public void FindClosest_EmptyTable_ReturnsEmpty()
    {
        var table   = CreateTable();
        var closest = table.FindClosest(NodeId(0x01));
        Assert.AreEqual(0, closest.Count);
    }

    [TestMethod]
    public void FindClosest_SingleEntry_ReturnsThatEntry()
    {
        var table = CreateTable();
        var entry = Entry(0x05);
        table.AddOrUpdate(entry);

        var closest = table.FindClosest(NodeId(0x05), k: 1);

        Assert.AreEqual(1, closest.Count);
        Assert.IsTrue(closest[0].NodeId.SequenceEqual(entry.NodeId));
    }

    // ── GetAddressForNode ────────────────────────────────────────────────────

    [TestMethod]
    public void GetAddressForNode_KnownNode_ReturnsAddress()
    {
        var table = CreateTable();
        var entry = Entry(0x07, 4242);
        table.AddOrUpdate(entry);

        var addr = table.GetAddressForNode(NodeId(0x07));

        Assert.IsNotNull(addr);
        Assert.AreEqual(4242, addr!.Port);
    }

    [TestMethod]
    public void GetAddressForNode_UnknownNode_ReturnsNull()
    {
        var table = CreateTable();
        Assert.IsNull(table.GetAddressForNode(NodeId(0xAB)));
    }

    // ── Bucket count sanity ──────────────────────────────────────────────────

    [TestMethod]
    public void BucketCount_DefaultIs256()
    {
        var table = CreateTable();
        Assert.AreEqual(256, table.BucketCount);
    }
}

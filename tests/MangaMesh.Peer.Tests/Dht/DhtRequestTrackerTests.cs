using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Node;
using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Tests.Dht;

[TestClass]
public class DhtRequestTrackerTests
{
    // ── DhtMessage (non-content) tracking ────────────────────────────────────

    [TestMethod]
    public void Register_ThenTryComplete_ResolvesTask()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();
        var tcs     = new TaskCompletionSource<DhtMessage>();

        tracker.Register(id, tcs);
        var reply = new DhtMessage { RequestId = id };
        bool completed = tracker.TryComplete(id, reply);

        Assert.IsTrue(completed);
        Assert.IsTrue(tcs.Task.IsCompletedSuccessfully);
        Assert.AreSame(reply, tcs.Task.Result);
    }

    [TestMethod]
    public void TryComplete_UnknownId_ReturnsFalse()
    {
        var tracker = new DhtRequestTracker();
        bool result = tracker.TryComplete(Guid.NewGuid(), new DhtMessage());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryComplete_AfterCancel_ReturnsFalse()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();
        var tcs     = new TaskCompletionSource<DhtMessage>();

        tracker.Register(id, tcs);
        tracker.Cancel(id);

        bool result = tracker.TryComplete(id, new DhtMessage());
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Cancel_RegisteredRequest_RemovesIt()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();
        var tcs     = new TaskCompletionSource<DhtMessage>();

        tracker.Register(id, tcs);
        tracker.Cancel(id);

        // TryComplete should return false — entry is gone
        Assert.IsFalse(tracker.TryComplete(id, new DhtMessage()));
    }

    [TestMethod]
    public void Cancel_UnknownId_DoesNotThrow()
    {
        var tracker = new DhtRequestTracker();
        tracker.Cancel(Guid.NewGuid()); // no exception
    }

    [TestMethod]
    public void TryComplete_CalledTwice_SecondCallReturnsFalse()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();
        var tcs     = new TaskCompletionSource<DhtMessage>();

        tracker.Register(id, tcs);
        tracker.TryComplete(id, new DhtMessage());
        bool second = tracker.TryComplete(id, new DhtMessage());

        Assert.IsFalse(second);
    }

    // ── ContentMessage tracking ──────────────────────────────────────────────

    [TestMethod]
    public void RegisterContent_ThenTryCompleteContent_ResolvesTask()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();
        var tcs     = new TaskCompletionSource<ContentMessage>();

        tracker.RegisterContent(id, tcs);
        var reply   = new ManifestData { ContentHash = "test" };
        bool result = tracker.TryCompleteContent(id, reply);

        Assert.IsTrue(result);
        Assert.IsTrue(tcs.Task.IsCompletedSuccessfully);
    }

    [TestMethod]
    public void TryCompleteContent_UnknownId_ReturnsFalse()
    {
        var tracker = new DhtRequestTracker();
        Assert.IsFalse(tracker.TryCompleteContent(Guid.NewGuid(), new ManifestData()));
    }

    [TestMethod]
    public void CancelContent_RegisteredRequest_RemovesIt()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();
        var tcs     = new TaskCompletionSource<ContentMessage>();

        tracker.RegisterContent(id, tcs);
        tracker.CancelContent(id);

        Assert.IsFalse(tracker.TryCompleteContent(id, new ManifestData()));
    }

    // ── DhtMessage and ContentMessage are independent ────────────────────────

    [TestMethod]
    public void DhtAndContentTracking_AreIndependent()
    {
        var tracker = new DhtRequestTracker();
        var id      = Guid.NewGuid();

        var dtcs = new TaskCompletionSource<DhtMessage>();
        var ctcs = new TaskCompletionSource<ContentMessage>();

        tracker.Register(id, dtcs);
        tracker.RegisterContent(id, ctcs);

        tracker.Cancel(id); // cancels only the DhtMessage entry

        // ContentMessage entry should still be alive
        Assert.IsTrue(tracker.TryCompleteContent(id, new ManifestData()));
    }
}

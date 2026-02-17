using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Transport;
using System.Collections.Concurrent;

namespace MangaMesh.Peer.Core.Node
{
    public sealed class DhtRequestTracker : IDhtRequestTracker
    {
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<DhtMessage>> _pending = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ContentMessage>> _pendingContent = new();

        public void Register(Guid requestId, TaskCompletionSource<DhtMessage> tcs)
            => _pending[requestId] = tcs;

        public bool TryComplete(Guid requestId, DhtMessage message)
        {
            if (_pending.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(message);
                return true;
            }
            return false;
        }

        public void Cancel(Guid requestId) => _pending.TryRemove(requestId, out _);

        public void RegisterContent(Guid requestId, TaskCompletionSource<ContentMessage> tcs)
            => _pendingContent[requestId] = tcs;

        public bool TryCompleteContent(Guid requestId, ContentMessage message)
        {
            if (_pendingContent.TryRemove(requestId, out var tcs))
            {
                tcs.TrySetResult(message);
                return true;
            }
            return false;
        }

        public void CancelContent(Guid requestId) => _pendingContent.TryRemove(requestId, out _);
    }
}

using MangaMesh.Peer.Core.Content;
using MangaMesh.Peer.Core.Transport;

namespace MangaMesh.Peer.Core.Node
{
    public interface IDhtRequestTracker
    {
        void Register(Guid requestId, TaskCompletionSource<DhtMessage> tcs);
        bool TryComplete(Guid requestId, DhtMessage message);
        void Cancel(Guid requestId);

        void RegisterContent(Guid requestId, TaskCompletionSource<ContentMessage> tcs);
        bool TryCompleteContent(Guid requestId, ContentMessage message);
        void CancelContent(Guid requestId);
    }
}

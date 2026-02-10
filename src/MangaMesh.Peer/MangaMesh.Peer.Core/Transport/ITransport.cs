using System;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public interface ITransport
    {
        Task SendAsync(NodeAddress to, ReadOnlyMemory<byte> payload);
        event Func<NodeAddress, ReadOnlyMemory<byte>, Task> OnMessage;
        int Port { get; }
    }
}

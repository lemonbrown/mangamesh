using System;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public interface IProtocolHandler
    {
        ProtocolKind Kind { get; }
        Task HandleAsync(NodeAddress from, ReadOnlyMemory<byte> payload);
    }
}

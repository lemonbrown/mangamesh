using System;
using System.Threading.Tasks;

namespace MangaMesh.Client.Transport
{
    public interface IProtocolHandler
    {
        ProtocolKind Kind { get; }
        Task HandleAsync(NodeAddress from, ReadOnlyMemory<byte> payload);
    }
}

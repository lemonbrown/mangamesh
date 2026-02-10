using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MangaMesh.Client.Transport
{
    public class ProtocolRouter
    {
        private readonly Dictionary<ProtocolKind, IProtocolHandler> _handlers = new();

        public void Register(IProtocolHandler handler)
        {
            _handlers[handler.Kind] = handler;
        }

        public async Task RouteAsync(NodeAddress from, ReadOnlyMemory<byte> payload)
        {
            if (payload.Length == 0) return;

            var kind = (ProtocolKind)payload.Span[0];
            if (_handlers.TryGetValue(kind, out var handler))
            {
                await handler.HandleAsync(from, payload.Slice(1));
            }
            else
            {
                Console.WriteLine($"Unknown protocol kind: {kind}");
            }
        }
    }
}

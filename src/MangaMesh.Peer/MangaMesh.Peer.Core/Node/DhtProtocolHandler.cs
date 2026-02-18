using MangaMesh.Peer.Core.Transport;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public class DhtProtocolHandler : IProtocolHandler
    {
        public IDhtNode? DhtNode { get; set; }

        public DhtProtocolHandler()
        {
        }

        public ProtocolKind Kind => ProtocolKind.Dht;

        public async Task HandleAsync(NodeAddress from, ReadOnlyMemory<byte> payload)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(payload.Span);
                var message = JsonSerializer.Deserialize<DhtMessage>(json);
                if (message != null)
                {
                    message.ComputedSenderIp = from.Host;
                    if (DhtNode != null)
                    {
                        await DhtNode.HandleMessageAsync(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DHT Handler] Error: {ex.Message}");
            }
        }
    }
}

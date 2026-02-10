using MangaMesh.Client.Node;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Client.Transport
{
    public class DhtProtocolHandler : IProtocolHandler
    {
        private readonly IDhtNode _dhtNode;

        public DhtProtocolHandler(IDhtNode dhtNode)
        {
            _dhtNode = dhtNode;
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
                    await _dhtNode.HandleMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DHT Handler] Error: {ex.Message}");
            }
        }
    }
}

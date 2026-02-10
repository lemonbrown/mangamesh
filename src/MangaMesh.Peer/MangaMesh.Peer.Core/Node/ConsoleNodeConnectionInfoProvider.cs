using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public class ConsoleNodeConnectionInfoProvider : INodeConnectionInfoProvider
    {
        public Task<(string IP, int Port)> GetConnectionInfoAsync()
        {
            // Console client currently doesn't accept inbound connections needed for P2P
            // Returning placeholder values
            return Task.FromResult(("127.0.0.1", 0));
        }
    }
}

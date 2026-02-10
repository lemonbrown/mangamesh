using System;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public interface INodeConnectionInfoProvider
    {
        Task<(string IP, int Port)> GetConnectionInfoAsync();
    }
}

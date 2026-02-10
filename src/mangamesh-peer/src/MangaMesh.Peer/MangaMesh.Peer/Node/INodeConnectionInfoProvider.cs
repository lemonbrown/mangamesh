using System;
using System.Threading.Tasks;

namespace MangaMesh.Client.Node
{
    public interface INodeConnectionInfoProvider
    {
        Task<(string IP, int Port)> GetConnectionInfoAsync();
    }
}

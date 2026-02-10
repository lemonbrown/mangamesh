using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Node
{
    public interface INodeIdentityService
    {
        string NodeId { get; }
        bool IsConnected { get; }
        DateTime? LastPingUtc { get; }
        void UpdateStatus(bool isConnected);
    }
}

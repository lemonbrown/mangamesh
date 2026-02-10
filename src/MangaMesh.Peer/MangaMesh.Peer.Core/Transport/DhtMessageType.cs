using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{

    public enum DhtMessageType
    {
        Ping,
        Pong,
        FindNode,
        Nodes,
        Store,
        FindValue,
        Value
    }
}

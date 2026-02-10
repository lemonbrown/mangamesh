using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public class RoutingEntry
    {
        public byte[] NodeId { get; set; }
        public NodeAddress Address { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}

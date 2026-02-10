using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Tracker
{
    public record AnnounceRequest(
      string NodeId,
      string IP,
      int Port,
      List<string> Manifests
  );
}

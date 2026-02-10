using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Keys
{
    public sealed record KeyPairResult(string PrivateKeyBase64, string PublicKeyBase64);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Keys
{
    public class KeyChallengeRequest
    {
        public string NonceBase64 { get; set; } = "";

        public string PrivateKeyBase64 { get; set; } = "";
    }
}

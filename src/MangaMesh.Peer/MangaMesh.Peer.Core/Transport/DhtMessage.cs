using MangaMesh.Peer.Core.Helpers;
using MangaMesh.Peer.Core.Node;
using NSec.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Transport
{
    public class DhtMessage
    {
        public DhtMessageType Type { get; set; }
        public byte[] SenderNodeId { get; set; }
        public byte[] Payload { get; set; }
        public DateTime TimestampUtc { get; set; }
        public byte[] Signature { get; set; }
        
        public Guid RequestId { get; set; } = Guid.NewGuid();

        public int SenderPort { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string ComputedSenderIp { get; set; } = string.Empty;

        public bool Verify(INodeIdentity senderIdentity)
        {
            return senderIdentity.Verify(
                Crypto.Hash(Type.ToString(), Payload, TimestampUtc, RequestId.ToByteArray()),
                Signature
            );
        }
    }
}

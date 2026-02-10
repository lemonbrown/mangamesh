using MangaMesh.Client.Blob;
using MangaMesh.Client.Transport;
using MangaMesh.Client.Node;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text;

namespace MangaMesh.Client.Content
{
    public class ContentProtocolHandler : IProtocolHandler
    {
        private readonly ITransport _transport;
        private readonly IBlobStore _blobStore;

        public IDhtNode? DhtNode { get; set; }

        public ContentProtocolHandler(ITransport transport, IBlobStore blobStore)
        {
            _transport = transport;
            _blobStore = blobStore;
        }

        public ProtocolKind Kind => ProtocolKind.Content;

        public Task HandleAsync(NodeAddress from, ReadOnlyMemory<byte> payload)
        {
            var msg = ContentMessage.Deserialize(payload);

            return msg switch
            {
                GetManifest m => HandleManifestAsync(from, m),
                GetBlob b => HandleBlobAsync(from, b),
                ManifestData d => HandleManifestDataAsync(from, d),
                BlobData bd => HandleBlobDataAsync(from, bd),
                _ => Task.CompletedTask
            };
        }

        private async Task HandleManifestAsync(NodeAddress from, GetManifest m)
        {
            // Try to find the manifest in the blob store using its hash
            // This assumes Manifests are stored as generic blobs, which is true in the new design
            var hash = new BlobHash(m.ContentHash);

            if (_blobStore.Exists(hash))
            {
                using var stream = await _blobStore.OpenReadAsync(hash);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var content = ms.ToArray();

                    var response = new ManifestData
                    {
                        ContentHash = m.ContentHash,
                        Data = content,
                        RequestId = m.RequestId
                    };

                    await SendResponseAsync(from, response, m.SenderPort);
                }
            }
        }

        private async Task HandleBlobAsync(NodeAddress from, GetBlob b)
        {
            var hash = new BlobHash(b.BlobHash);
            if (_blobStore.Exists(hash))
            {
                using var stream = await _blobStore.OpenReadAsync(hash);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var content = ms.ToArray();

                    var response = new BlobData
                    {
                        BlobHash = b.BlobHash,
                        Data = content,
                        RequestId = b.RequestId
                    };

                    await SendResponseAsync(from, response, b.SenderPort);
                }
            }
        }

        private Task HandleManifestDataAsync(NodeAddress from, ManifestData d)
        {
            DhtNode?.HandleContentMessage(d);
            return Task.CompletedTask;
        }

        private Task HandleBlobDataAsync(NodeAddress from, BlobData d)
        {
            DhtNode?.HandleContentMessage(d);
            return Task.CompletedTask;
        }

        private async Task SendResponseAsync(NodeAddress to, ContentMessage message, int senderPort)
        {
            var json = JsonSerializer.Serialize<ContentMessage>(message);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var payload = new byte[1 + jsonBytes.Length];
            payload[0] = (byte)ProtocolKind.Content;
            Array.Copy(jsonBytes, 0, payload, 1, jsonBytes.Length);

            var replyAddress = senderPort > 0
                ? new NodeAddress(to.Host, senderPort)
                : to;

            await _transport.SendAsync(replyAddress, new ReadOnlyMemory<byte>(payload));
        }
    }
}

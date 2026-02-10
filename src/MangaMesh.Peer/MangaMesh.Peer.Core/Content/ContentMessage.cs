using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MangaMesh.Peer.Core.Content
{
    [JsonDerivedType(typeof(GetManifest), typeDiscriminator: "GetManifest")]
    [JsonDerivedType(typeof(GetBlob), typeDiscriminator: "GetBlob")]
    [JsonDerivedType(typeof(ManifestData), typeDiscriminator: "ManifestData")]
    [JsonDerivedType(typeof(BlobData), typeDiscriminator: "BlobData")]
    public abstract class ContentMessage
    {
        public int SenderPort { get; set; }
        public Guid RequestId { get; set; } = Guid.NewGuid();

        public static ContentMessage? Deserialize(ReadOnlyMemory<byte> payload)
        {
            try 
            {
                var json = System.Text.Encoding.UTF8.GetString(payload.Span);
                return JsonSerializer.Deserialize<ContentMessage>(json);
            }
            catch { return null; }
        }

        public byte[] Serialize()
        {
            return JsonSerializer.SerializeToUtf8Bytes(this);
        }
    }

    public class GetManifest : ContentMessage
    {
        public string ContentHash { get; set; } = string.Empty;
    }

    public class GetBlob : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
    }

    public class ManifestData : ContentMessage
    {
        public string ContentHash { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public class BlobData : ContentMessage
    {
        public string BlobHash { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}

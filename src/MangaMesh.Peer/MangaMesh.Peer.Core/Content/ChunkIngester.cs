using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Shared.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Content
{
    public interface IChunkIngester
    {
        Task<(PageManifest Manifest, string ManifestHash)> IngestAsync(Stream dataStream, string mimeType);
    }

    public class ChunkIngester : IChunkIngester
    {
        private readonly IBlobStore _blobStore;
        private readonly int _chunkSize;

        public ChunkIngester(IBlobStore blobStore, IOptions<DhtOptions> options)
        {
            _blobStore = blobStore;
            _chunkSize = options.Value.ChunkSizeBytes;
        }

        public async Task<(PageManifest Manifest, string ManifestHash)> IngestAsync(Stream dataStream, string mimeType)
        {
            var fileSize = dataStream.Length;
            var chunks = new List<string>();
            var buffer = new byte[_chunkSize];
            int bytesRead;

            // Ensure stream is at beginning
            if (dataStream.CanSeek)
                dataStream.Position = 0;

            while ((bytesRead = await dataStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                // Create a memory stream for the chunk
                // If bytesRead < buffer.Length, we need to resize to avoid trailing zeros
                // or just pass the segment. IBlobStore.PutAsync takes a Stream.
                
                MemoryStream chunkStream;
                if (bytesRead == buffer.Length)
                {
                    chunkStream = new MemoryStream(buffer, 0, bytesRead, writable: false, publiclyVisible: true);
                }
                else
                {
                    var partialBuffer = new byte[bytesRead];
                    Array.Copy(buffer, partialBuffer, bytesRead);
                    chunkStream = new MemoryStream(partialBuffer);
                }

                using (chunkStream)
                {
                    // Store the chunk (computes hash internally)
                    var blobHash = await _blobStore.PutAsync(chunkStream);
                    chunks.Add(blobHash.Value);
                }
            }

            var manifest = new PageManifest
            {
                Version = 1,
                MimeType = mimeType,
                FileSize = fileSize,
                ChunkSize = _chunkSize,
                Chunks = chunks
            };

            var json = JsonSerializer.Serialize(manifest);
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            // Compute hash of the manifest
            var manifestHashString = Convert.ToHexString(SHA256.HashData(jsonBytes)).ToLowerInvariant();

            // Store the manifest itself in the blob store
            using var manifestStream = new MemoryStream(jsonBytes);
            await _blobStore.PutAsync(manifestStream);

            return (manifest, manifestHashString);
        }
    }
}

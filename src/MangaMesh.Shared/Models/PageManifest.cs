using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Models
{
    public sealed record PageManifest
    {
        public int Version { get; init; } = 1;
        public string MimeType { get; init; } = "application/octet-stream";
        public long FileSize { get; init; }
        public int ChunkSize { get; init; } = 262144; // 256KB
        public IReadOnlyList<string> Chunks { get; init; } = Array.Empty<string>();
    }
}

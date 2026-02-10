using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Models
{
    public sealed record SignedChapterManifest
    {
        public ChapterManifest Manifest { get; init; } = default!;

        public string ManifestHash { get; init; } = default!;
        public string Signature { get; init; } = default!;
        public string PublisherPublicKey { get; init; } = default!;
    }

}

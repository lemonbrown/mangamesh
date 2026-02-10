using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Chapters
{
    /// <summary>
    /// Result returned after importing a chapter
    /// </summary>
    public class ImportChapterResult
    {
        /// <summary>
        /// Manifest hash for the chapter that was created
        /// </summary>
        public ManifestHash ManifestHash { get; init; } = default!;

        /// <summary>
        /// Number of files imported successfully
        /// </summary>
        public int FileCount { get; init; }

        public bool AlreadyExists { get; set; }
    }
}

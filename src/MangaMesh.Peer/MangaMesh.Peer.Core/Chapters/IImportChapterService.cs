using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Chapters
{
    public interface IImportChapterService
    {
        public Task<ImportChapterResult> ImportAsync(ImportChapterRequest request, CancellationToken ct = default);
        Task ReannounceAsync(ManifestHash hash, string nodeId);
    }
}

using MangaMesh.Client.Manifests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Chapters
{
    public interface IImportChapterService
    {
        public Task<ImportChapterResult> ImportAsync(ImportChapterRequest request, CancellationToken ct = default);
        Task ReannounceAsync(ManifestHash hash, string nodeId);
    }
}

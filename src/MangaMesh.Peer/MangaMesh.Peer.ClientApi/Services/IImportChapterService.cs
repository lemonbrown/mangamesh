using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.ClientApi.Services
{
    public interface IImportChapterService
    {
        /// <summary>
        /// Import a chapter into the local node.
        /// Generates the manifest, stores files, and publishes metadata.
        /// </summary>
        /// <param name="request">Import request DTO</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Result including manifest hash and file count</returns>
        Task<ImportResultDto> ImportAsync(
            ImportChapterRequestDto request,
            CancellationToken ct = default
        );

        Task ReannounceAsync(ManifestHash hash, string nodeId);
    }

}

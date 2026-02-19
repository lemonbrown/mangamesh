using MangaMesh.Peer.ClientApi.Models;
using MangaMesh.Peer.Core.Chapters;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.ClientApi.Services
{
    public class ImportChapterServiceWrapper : IImportChapterService
    {
        private readonly ImportChapterService _importService;

        public ImportChapterServiceWrapper(ImportChapterService importService)
        {
            _importService = importService;
        }

        public async Task<ImportResultDto> ImportAsync(ImportChapterRequestDto request, CancellationToken ct = default)
        {
            var result = await _importService.ImportAsync(new ImportChapterRequest
            {
                SeriesId = request.SeriesId,
                ScanlatorId = request.ScanlatorId,
                Language = request.Language,
                ChapterNumber = request.ChapterNumber,
                SourceDirectory = request.SourcePath,
                ReleaseType = request.ReleaseType switch
                {
                    "VerifiedScanlation" => ReleaseType.VerifiedScanlation,
                    "UnverifiedScanlation" => ReleaseType.UnverifiedScanlation,
                    "RoughTranslation" => ReleaseType.RoughTranslation,
                    "Raw" => ReleaseType.Raw,
                    _ => ReleaseType.Unknown
                },
                DisplayName = request.DisplayName,
                Source = request.Source,
                ExternalMangaId = request.ExternalMangaId,
                Quality = request.Quality
            }, ct);

            return new ImportResultDto(
                ManifestHash: result.ManifestHash.Value,
                FilesImported: result.FileCount
            );
        }

        public async Task ReannounceAsync(ManifestHash hash, string nodeId)
        {
            await _importService.ReannounceAsync(hash, nodeId);
        }
    }

}

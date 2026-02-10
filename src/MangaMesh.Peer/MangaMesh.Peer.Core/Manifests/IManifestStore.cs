using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Manifests
{
    public interface IManifestStore
    {

        public Task SaveAsync(ManifestHash hash, ChapterManifest manifest);
        Task<ManifestHash> PutAsync(ChapterManifest manifest);
        Task<ChapterManifest?> GetAsync(ManifestHash hash);
        Task<ChapterManifest?> GetBySeriesAndChapterIdAsync(string seriesId, string chapterId);
        Task<(string SetHash, int Count)> GetSetHashAsync();
        Task<bool> ExistsAsync(ManifestHash manifestHash);
        Task<IEnumerable<ManifestHash>> GetAllHashesAsync();
        Task DeleteAsync(ManifestHash hash);
    }
}

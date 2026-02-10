using MangaMesh.Peer.Core.Chapters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Metadata
{
    public interface IMetadataClient
    {
        /// <summary>
        /// Publish metadata for a chapter to peers or trackers
        /// </summary>
        //Task PublishAsync(ChapterMetadata metadata, CancellationToken ct = default);

        Task<IReadOnlyList<ChapterMetadata>> GetChaptersAsync(
            string seriesId,
            string language,
            CancellationToken ct = default
        );
    }
}

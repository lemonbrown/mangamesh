using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Chapters
{
    public sealed class Chapter
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public Guid MangaId { get; init; }

        public string ExternalSource { get; init; } = "mangadex";
        public string ExternalId { get; init; } = default!;

        public string? Title { get; init; }
        public string? ChapterNumber { get; init; }
        public string? Volume { get; init; }
        public string Language { get; init; } = default!;

        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    }

}

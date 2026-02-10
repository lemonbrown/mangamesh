using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Models
{
    public sealed record ChapterFileEntry
    {
        public string Path { get; init; } = default!;
        public long Size { get; init; }
        public string Hash { get; init; } = default!;
    }

}

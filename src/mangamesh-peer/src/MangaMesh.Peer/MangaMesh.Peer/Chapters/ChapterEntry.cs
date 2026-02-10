using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Chapters
{
    public record ChapterEntry(string SeriesId, int ChapterNumber, string ManifestHash);

}

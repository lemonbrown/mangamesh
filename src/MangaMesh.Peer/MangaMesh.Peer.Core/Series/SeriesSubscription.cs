using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Series
{
    public record SeriesSubscription
    {
        public string SeriesId { get; set; } = "";

        public string Language { get; set; } = "";

        public bool AutoFetch { get; init; } = true;

        public List<string> AutoFetchScanlators { get; set; } = new();

        public DateTime SubscribedAt { get; set; }
    }

}

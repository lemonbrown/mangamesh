using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Node
{
    public interface IPeerFetcher
    {
        Task<ManifestHash> FetchManifestAsync(string manifestHash);
    }
}

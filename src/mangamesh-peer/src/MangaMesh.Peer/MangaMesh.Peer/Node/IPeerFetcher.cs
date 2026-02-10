using MangaMesh.Client.Manifests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Client.Node
{
    public interface IPeerFetcher
    {
        Task<ManifestHash> FetchManifestAsync(string manifestHash);
    }
}

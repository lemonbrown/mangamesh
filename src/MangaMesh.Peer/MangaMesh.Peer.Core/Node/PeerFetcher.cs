
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MangaMesh.Peer.Core.Blob;
using MangaMesh.Peer.Core.Manifests;
using MangaMesh.Peer.Core.Tracker;
using MangaMesh.Shared.Models;

namespace MangaMesh.Peer.Core.Node
{

    public sealed class PeerFetcher : IPeerFetcher
    {
        private readonly ITrackerClient _trackerClient;
        private readonly IBlobStore _blobStore;
        private readonly IManifestStore _manifestStore;
        private readonly HttpClient _httpClient;

        public PeerFetcher(
            ITrackerClient trackerClient,
            IBlobStore blobStore,
            IManifestStore manifestStore)
        {
            _trackerClient = trackerClient;
            _blobStore = blobStore;
            _manifestStore = manifestStore;
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            _httpClient = new HttpClient(handler);
        }

        public async Task<ManifestHash> FetchManifestAsync(string manifestHash)
        {
            throw new NotImplementedException("P2P fetching temporarily disabled during IP removal refactor.");
        }
    }
}

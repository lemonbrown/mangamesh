using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Blob
{
    public interface IBlobStore
    {
        Task<BlobHash> PutAsync(Stream data);
        Task<Stream?> OpenReadAsync(BlobHash hash);
        bool Exists(BlobHash hash);
        IEnumerable<BlobHash> GetAllHashes();
    }
}

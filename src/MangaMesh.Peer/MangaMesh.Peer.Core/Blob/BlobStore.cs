using MangaMesh.Peer.Core.Configuration;
using MangaMesh.Peer.Core.Storage;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Peer.Core.Blob
{
    public sealed class BlobStore : IBlobStore
    {
        private readonly string _root;
        private readonly IStorageMonitorService _storageMonitor;

        public BlobStore(IOptions<BlobStoreOptions> options, IStorageMonitorService storageMonitor)
        {
            _root = options.Value.RootPath;
            _storageMonitor = storageMonitor;
            Directory.CreateDirectory(_root);
        }

        public async Task<BlobHash> PutAsync(Stream data)
        {
            using var sha = SHA256.Create();
            using var temp = new MemoryStream();

            await data.CopyToAsync(temp);
            temp.Position = 0;

            var hashBytes = sha.ComputeHash(temp);
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            var blobHash = new BlobHash(hash);

            var path = GetPath(blobHash);
            if (File.Exists(path))
                return blobHash;

            await _storageMonitor.EnsureStorageAvailable(temp.Length);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            temp.Position = 0;
            var tmpPath = path + ".tmp";

            await using (var fs = File.Create(tmpPath))
                await temp.CopyToAsync(fs);

            File.Move(tmpPath, path, overwrite: false);

            _storageMonitor.NotifyBlobWritten(temp.Length);

            return blobHash;
        }

        public async Task<Stream?> OpenReadAsync(BlobHash hash)
        {
            var path = GetPath(hash);
            if (!File.Exists(path))
                return null;

            return File.OpenRead(path);
        }

        public bool Exists(BlobHash hash)
            => File.Exists(GetPath(hash));

        public long GetSize(BlobHash hash)
        {
            var path = GetPath(hash);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        public void Delete(BlobHash hash)
        {
            var path = GetPath(hash);
            if (File.Exists(path))
                File.Delete(path);
        }

        public IEnumerable<BlobHash> GetAllHashes()
        {
            if (!Directory.Exists(_root))
                yield break;

            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                if (name.Length == 64 && !name.EndsWith(".tmp"))
                    yield return new BlobHash(name);
            }
        }

        private string GetPath(BlobHash hash)
        {
            var a = hash.Value[..2];
            var b = hash.Value[2..4];
            return Path.Combine(_root, a, b, hash.Value);
        }
    }

}

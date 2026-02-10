using MangaMesh.Client.Manifests;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MangaMesh.Client.Storage
{
    public class StorageMonitorService : IStorageMonitorService
    {
        private readonly string _inputPath;
        private readonly IManifestStore _manifestStore;
        private const long MaxStorageBytes = 5L * 1024 * 1024 * 1024; // 5 GB

        // Simple cache
        private long? _cachedUsedBytes;
        private DateTime _lastScan = DateTime.MinValue;

        public StorageMonitorService(string inputPath, IManifestStore manifestStore)
        {
            _inputPath = inputPath;
            _manifestStore = manifestStore;
        }

        public async Task<StorageStats> GetStorageStatsAsync()
        {
            var manifestCount = (await _manifestStore.GetAllHashesAsync()).Count();
            var usedBytes = GetUsedBytes();

            return new StorageStats
            {
                TotalMb = MaxStorageBytes / (1024.0 * 1024.0),
                UsedMb = usedBytes / (1024.0 * 1024.0),
                ManifestCount = manifestCount
            };
        }

        public Task EnsureStorageAvailable(long bytesRequired)
        {
            var used = GetUsedBytes();
            if (used + bytesRequired > MaxStorageBytes)
            {
                throw new IOException($"Storage limit exceeded. Limit: {MaxStorageBytes} bytes, Used: {used} bytes, Required: {bytesRequired} bytes.");
            }
            return Task.CompletedTask;
        }

        public void NotifyBlobWritten(long bytes)
        {
            if (_cachedUsedBytes.HasValue)
            {
                _cachedUsedBytes += bytes;
            }
        }

        private long GetUsedBytes()
        {
            // Re-scan if cache is empty or stale (> 5 minutes? Or just rely on notifications?)
            // For now, let's scan on first use, then rely on updates + periodic rescan (e.g. 10 mins)
            if (!_cachedUsedBytes.HasValue || (DateTime.UtcNow - _lastScan).TotalMinutes > 10)
            {
                if (Directory.Exists(_inputPath))
                {
                    _cachedUsedBytes = CalculateDirSize(new DirectoryInfo(_inputPath));
                }
                else
                {
                    _cachedUsedBytes = 0;
                }
                _lastScan = DateTime.UtcNow;
            }
            return _cachedUsedBytes.Value;
        }

        private long CalculateDirSize(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += CalculateDirSize(di);
            }
            return size;
        }
    }
}

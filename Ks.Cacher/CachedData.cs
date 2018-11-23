using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Ks.Cacher
{
    public sealed class CachedData : IDisposable
    {
        internal CachedData(string key)
        {
            Key = key;
        }

        internal event EventHandler Disposed;
        
        public string Key { get; internal set; }
        
        public string Path { get; internal set; }

        public long Size { get; internal set; }

        public bool Exists => File.Exists(Path);

        public bool Locked => StreamLock || UserLock;

        private bool UserLock { get; set; }

        internal SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);

        internal bool StreamLock { get; set; } = false;

        public void Lock()
        {
            UserLock = true;
        }

        public void Unlock()
        {
            UserLock = false;
        }

        public async Task<CachedStream> CreateStreamAsync()
        {
            await Semaphore.WaitAsync();

            StreamLock = true;
            return new CachedStream(this);
        }

        public void Dispose()
        {
            Semaphore?.Dispose();
            Disposed?.Invoke(this, EventArgs.Empty);
        }
    }
}

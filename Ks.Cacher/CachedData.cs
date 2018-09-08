using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ks.Cacher
{
    public class CachedData
    {
        internal CachedData()
        {
            
        }
        
        public string Path { get; internal set; }

        public long Size { get; internal set; }

        public bool Exists => File.Exists(Path);

        public bool Locked { get; private set; } = true;

        public SemaphoreSlim CachingSemaphore { get; } = new SemaphoreSlim(1, 1);

        public void Lock()
        {
            Locked = true;
        }

        public void Unlock()
        {
            Locked = false;
        }

        public async Task<CachedStream> CreateStreamAsync()
        {
            await CachingSemaphore.WaitAsync();
            return new CachedStream(this);
        }
    }

    public class CachedStream : Stream
    {
        private FileStream BaseStream { get; }
        
        public CachedData Cache { get; }
        
        internal CachedStream(CachedData cache)
        {
            BaseStream = new FileStream(cache.Path, FileMode.Open, FileAccess.Read);
            Cache = cache;
        }

        public override void Flush()
        {
            throw new System.NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return BaseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            BaseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotSupportedException();
        }

        public override bool CanRead => BaseStream.CanRead;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                Cache.CachingSemaphore.Release();
            }
        }
    }
}

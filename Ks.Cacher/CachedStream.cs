using System.IO;

namespace Ks.Cacher
{
    public class CachedStream : Stream
    {
        private FileStream BaseStream { get; set; }
        
        public CachedData Cache { get; private set; }
        
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

            if (BaseStream != null)
            {
                BaseStream.Dispose();
                BaseStream = null;
            }

            if (Cache != null)
            {
                Cache.Semaphore.Release();
                Cache.StreamLock = false;
                Cache = null;
            }
        }
    }
}
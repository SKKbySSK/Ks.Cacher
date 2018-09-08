namespace Ks.Cacher
{
    public class CachedData
    {
        public CachedData(string path, long size)
        {
            Path = path;
            Size = size;
        }

        public string Path { get; }

        public long Size { get; }

        public bool Exists => System.IO.File.Exists(Path);

        public bool Locked { get; internal set; } = true;

        public void Lock()
        {
            Locked = true;
        }

        public void Unlock()
        {
            Locked = false;
        }
    }
}

namespace Ks.Cacher
{
    public class FileCacheConfig
    {
        private FileCacheConfig(string directory)
        {
            Directory = directory;
        }

        public CacheMode Mode { get; private set; }

        public CacheRemovingPriority RemovingPriority { get; set; } = CacheRemovingPriority.SmallFile;

        public string Directory { get; private set; }

        public int MaximumCount { get; private set; }

        //public static FileCacheConfig CreateSizeConfig(string directory, long size = 100 * 1024 * 1024)
        //{
        //    return new FileCacheConfig(directory)
        //    {
        //        Mode = CacheMode.Size,
        //        MaximumSize = size
        //    };
        //}

        public static FileCacheConfig CreateCountConfig(string directory, int count = 10)
        {
            return new FileCacheConfig(directory)
            {
                Mode = CacheMode.Count,
                MaximumCount = count
            };
        }
    }

    public enum CacheMode
    {
        Size,
        Count
    }

    public enum CacheRemovingPriority
    {
        LargeFile,
        SmallFile
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ks.Cacher
{
    public class FileManager
    {
        private readonly object lockObj = new object();

        private Dictionary<string, CachedData> Caches { get; } = new Dictionary<string, CachedData>();

        private Dictionary<string, CachedData> Caching { get; } = new Dictionary<string, CachedData>();

        public event EventHandler<CachedEventArgs> Cached;

        public event EventHandler<CachedEventArgs> Removed;

        public FileManager(FileCacheConfig config, bool clear)
        {
            Config = config;
            Directory.CreateDirectory(config.Directory);

            if (clear)
            {
                var files = Directory.GetFiles(config.Directory);
                foreach (var f in files)
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch (Exception ex)
                    {
                        Logger?.OnExceptionThrown(ex);
                    }
                }
            }
        }

        public long TotalSize { get; private set; } = 0;

        public int TotalCount { get; private set; } = 0;

        public ILogger Logger { get; set; }

        public FileCacheConfig Config { get; }

        /// <summary>
        /// Get the <see cref="CachedData"/> from caches if it exists. Otherwise, create new one.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public async Task<CachedData> GetDataAsync(string key, CacheFactory factory, bool lockData)
        {
            var data = GetData(key, lockData);
            
            if(data == null)
                data = await CacheAsync(key, factory, lockData).ConfigureAwait(false);
            
            return data;
        }

        /// <summary>
        /// Get the <see cref="CachedData"/> from caches if it exists
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lockData"></param>
        /// <returns></returns>
        public CachedData GetData(string key, bool lockData)
        {
            lock (lockObj)
            {
                if (Caches.ContainsKey(key))
                {
                    var data = Caches[key];

                    if (lockData)
                        data.Lock();

                    return data;
                }
            }

            return null;
        }

        /// <summary>
        /// Enforce the manager to add/update cache from <see cref="CacheFactory"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public async Task<CachedData> CacheAsync(string key, CacheFactory factory, bool lockData)
        {
            CachedData data = new CachedData(key);
            await data.Semaphore.WaitAsync();

            CachedData caching = null;
            
            lock (lockObj)
            {
                if (Caching.ContainsKey(key))
                {
                    caching = Caching[key];
                }
                else
                {
                    data.Lock();
                    Caching.Add(key, data);
                }
            }

            if (caching != null)
            {
                await caching.Semaphore.WaitAsync();
                caching.Semaphore.Release();
                return caching;
            }

            using (var fs = AllocateTemporaryFile(factory, out var path))
            {
                await factory.Factory(fs);
                    
                data.Path = path;
                data.Size = fs.Length;
                
                TotalSize += fs.Length;
                TotalCount++;
            }

            CachedData last = null;
            
            lock (lockObj)
            {
                Caching.Remove(key);

                if (Caches.ContainsKey(key))
                    last = Caches[key];

                data.Disposed += DataOnDisposed;
                Caches[key] = data;
            }
            
            last?.Dispose();

            CheckCaches();
                
            if (!lockData) data.Unlock();
            data.Semaphore.Release();
            
            Cached?.Invoke(this, new CachedEventArgs(data));

            return data;
        }

        private void DataOnDisposed(object sender, EventArgs e)
        {
            var data = (CachedData) sender;
            data.Disposed -= DataOnDisposed;

            lock (lockObj)
            {
                if (Caches.ContainsKey(data.Key))
                {
                    File.Delete(data.Path);
                    TotalSize -= data.Size;
                    TotalCount--;
                    Caches.Remove(data.Key);

                    data.Path = null;
                    data.Key = null;
                }
            }
            
            Removed?.Invoke(this, new CachedEventArgs(data));
        }

        public async Task CopyAsync(CachedData data, string export)
        {
            data.Lock();
            
            try
            {
                using (var input = await data.CreateStreamAsync())
                using (FileStream dest = new FileStream(export, FileMode.Open, FileAccess.Write))
                {
                    await input.CopyToAsync(dest).ConfigureAwait(false);
                }
            }
            finally
            {
                data.Unlock();
            }
        }

        public void CheckCaches()
        {
            List<CachedData> dispose = new List<CachedData>();
            
            try
            {
                lock (lockObj)
                {
                    IEnumerable<KeyValuePair<string, CachedData>> order = Caches.OrderBy(pair => pair.Value.Size);
                    if (Config.RemovingPriority == CacheRemovingPriority.LargeFile) order = order.Reverse();
                    order = order.Where(d => !d.Value.Locked);
                    var rem = order.ToList();

                    switch (Config.Mode)
                    {
                        case CacheMode.Count:
                            var delta = TotalCount - Config.MaximumCount;
                            for (int i = 0; delta > i; i++)
                                dispose.Add(rem[i].Value);
                            break;
                        case CacheMode.Size:
                            Console.WriteLine("CacheMode.Size is currently unsupported");
                            break;
                    }
                }
            }
            catch(Exception ex)
            {
                Logger?.OnExceptionThrown(ex);
            }

            foreach (var data in dispose)
            {
                data.Dispose();
            }
        }

        private FileStream AllocateTemporaryFile(CacheFactory factory, out string path)
        {
            path = null;
            Random rnd = new Random();
            do
            {
                path = Path.Combine(Config.Directory, factory.GetFilename(Guid.NewGuid().ToString()));
            } while (File.Exists(path));

            return File.Create(path);
        }
    }
}

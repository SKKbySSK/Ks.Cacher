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
        private Dictionary<string, CachedData> Caches { get; } = new Dictionary<string, CachedData>();

        public event EventHandler<CachedEventArgs> Cached;

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
        /// Get the <see cref="CachedData"/> from caches if it exists. Otherwise, create new cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public async Task<CachedData> GetDataAsync(string key, CacheFactory factory, bool lockData)
        {
            CachedData data;
            lock (Caches)
            {
                if (Caches.ContainsKey(key))
                {
                    data = Caches[key];

                    if (lockData)
                        data.Lock();

                    return data;
                }
            }

            data = await CacheAsync(key, factory, lockData).ConfigureAwait(false);
            return data;
        }

        /// <summary>
        /// Enforce the manager to add/update cache from <see cref="CacheFactory"/>
        /// </summary>
        /// <param name="key"></param>
        /// <param name="factory"></param>
        /// <returns></returns>
        public async Task<CachedData> CacheAsync(string key, CacheFactory factory, bool lockData)
        {
            CachedData data = new CachedData();

            try
            {
                CachedData caching = null;
                Task semaphoreTask = null;
            
                lock (Caches)
                {
                    if (Caches.ContainsKey(key))
                    {
                        var last = Caches[key];
                        if (last.CachingSemaphore.Wait(0))
                        {
                            File.Delete(last.Path);
                            TotalSize -= last.Size;
                            TotalCount--;
                        }
                        else
                        {
                            caching = last;
                            semaphoreTask = caching.CachingSemaphore.WaitAsync();
                        }
                    }
                    else
                    {
                        semaphoreTask = data.CachingSemaphore.WaitAsync();
                        data.Lock();
                        Caches.Add(key, data);
                    }
                }

                if (semaphoreTask != null)
                    await semaphoreTask;
            
                if (caching != null)
                {
                    caching.CachingSemaphore.Release();
                    return caching;
                }
            
                using (var fs = AllocateTemporaryFile(factory, out var path))
                {
                    await factory.Factory(fs);
                
                    CheckCaches();
                    
                    data.Path = path;
                    data.Size = fs.Length;
                    
                    TotalSize += fs.Length;
                    TotalCount++;
                }
            
                return data;
            }
            finally
            {
                if (!lockData) data.Unlock();
                data.CachingSemaphore.Release();
                Cached?.Invoke(this, new CachedEventArgs(data));
            }
        }

        public async Task CopyAsync(CachedData data, string export)
        {
            await data.CachingSemaphore.WaitAsync();
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
                data.CachingSemaphore.Release();
                data.Unlock();
            }
        }

        public void CheckCaches()
        {
            lock (Caches)
            {
                try
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
                            {
                                var pair = rem[i];
                                File.Delete(pair.Value.Path);
                                TotalSize -= pair.Value.Size;
                                TotalCount--;
                                Caches.Remove(pair.Key);
                            }
                            break;
                        case CacheMode.Size:
                            Console.WriteLine("CacheMode.Size is currently unsupported");
                            break;
                    }
                }
                catch(Exception ex)
                {
                    Logger?.OnExceptionThrown(ex);
                }
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

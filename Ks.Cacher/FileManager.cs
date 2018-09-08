﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace Ks.Cacher
{
    public class CachedEventArgs : EventArgs
    {
        public CachedEventArgs(CachedData data)
        {
            Data = data;
        }

        public CachedData Data { get; }
    }

    public interface ILogger
    {
        void OnExceptionThrown(Exception ex);
    }

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
                        data.Locked = lockData;
                }
            }

            data = await CacheAsync(key, factory, lockData);
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
            CachedData data;
            using (var fs = AllocateTemporaryFile(factory, out var path))
            using (var stream = await factory.Factory().ConfigureAwait(false))
            {
                await stream.CopyToAsync(fs);

                data = new CachedData(path, fs.Length);
                data.Locked = true;

                lock (Caches)
                {
                    if (Caches.ContainsKey(key))
                    {
                        var last = Caches[key];
                        TotalSize -= new FileInfo(last.Path).Length;
                        TotalCount--;
                    }

                    TotalSize += fs.Length;
                    TotalCount++;
                    Caches[key] = data;
                }

                CheckCaches();
                
                Cached?.Invoke(this, new CachedEventArgs(data));

                if (!lockData) data.Locked = false;

                return data;
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
                            Console.WriteLine("CacheMode.Size is not currently supported");
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
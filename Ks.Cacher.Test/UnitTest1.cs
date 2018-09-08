using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Ks.Cacher.Test
{
    public class UnitTest1
    {
        [Theory(DisplayName = "個数上限付きテスト")]
        [InlineData(10, 100)]
        [InlineData(20, 300)]
        public async Task CountTest(int count, int size)
        {
            var man = new FileManager(FileCacheConfig.CreateCountConfig("Cache/", count), true);

            int offset = 0;
            offset += await GetData(offset, count, size, man);

            Assert.Equal(count, man.TotalCount);
            Assert.Equal(size * count, man.TotalSize);

            offset += await GetData(offset, count, size, man);

            Assert.Equal(count, man.TotalCount);
            Assert.Equal(size * count, man.TotalSize);

            offset = await TestLocked(count, size, man, offset);
        }

        private static async Task<int> TestLocked(int count, int size, FileManager man, int offset)
        {
            List<CachedData> dataCollection = new List<CachedData>();
            for (int i = 0; count > i; i++)
            {
                dataCollection.Add(await man.GetDataAsync((offset + i).ToString(), GetFactory(size), true));
            }
            offset += count;

            offset += await GetData(offset, count, size, man);
            Assert.Equal(count + 1, man.TotalCount);
            return offset;
        }

        private static async Task<int> GetData(int offset, int count, int size, FileManager man)
        {
            for (int i = 0; count > i; i++)
            {
                await man.GetDataAsync((offset + i).ToString(), GetFactory(size), false);
            }
            return count;
        }

        private static CacheFactory GetFactory(int size)
        {
            return new CacheFactory(() => Task.FromResult<System.IO.Stream>(new System.IO.MemoryStream(new byte[size])));
        }
    }
}

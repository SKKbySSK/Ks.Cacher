using System;
using System.Threading.Tasks;
using System.IO;

namespace Ks.Cacher
{
    public class CacheFactory
    {
        public CacheFactory(Func<Stream, Task> factory)
        {
            Factory = factory;
        }

        public CacheFactory(string prefix, string suffix, Func<Stream, Task> factory) : this(factory)
        {
            Prefix = prefix;
            Suffix = suffix;
        }

        public Func<Stream, Task> Factory { get; }

        public string Prefix { get; set; } = "cache";

        public string Suffix { get; set; } = ".tmp";

        internal string GetFilename(string body)
        {
            return Prefix + body + Suffix;
        }
    }
}

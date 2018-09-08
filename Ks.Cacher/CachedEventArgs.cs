using System;

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
}

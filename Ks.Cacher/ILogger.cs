using System;

namespace Ks.Cacher
{
    public interface ILogger
    {
        void OnExceptionThrown(Exception ex);
    }
}

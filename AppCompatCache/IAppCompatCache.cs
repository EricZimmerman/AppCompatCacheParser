using System.Collections.Generic;

namespace AppCompatCache
{
    public interface IAppCompatCache
    {
        List<CacheEntry> Entries { get;  }
    }
}
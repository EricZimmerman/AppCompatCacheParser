using System.Collections.Generic;

namespace AppCompatCache;

public interface IAppCompatCache
{
    List<CacheEntry> Entries { get; }

    /// <summary>
    ///     The total number of entries to expect
    /// </summary>
    /// <remarks>When not available (Windows 8.x/10), will return -1</remarks>
    int EntryCount { get; }

    int ControlSet { get; }
}
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

    /// <summary>Entries recovered from the slack of the value's cell (Windows 8 to 11) that are no longer in the cache</summary>
    List<CacheEntry> SlackEntries { get; }

    /// <summary>Entries that could not be read (the ones before them are in <see cref="Entries" />)</summary>
    List<string> ParseErrors { get; }
}

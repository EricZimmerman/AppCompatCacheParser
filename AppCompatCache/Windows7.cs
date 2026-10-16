using System.Collections.Generic;

namespace AppCompatCache;

/// <summary>
///     Windows 7 and 2008 R2 (signature 0xBADC0FEE): a 128 byte header with the number of entries at 4, then 32 byte
///     (32 bit) or 48 byte (64 bit) entries. Insertion flag 0x2 means executed
/// </summary>
public class Windows7 : IAppCompatCache
{
    public Windows7(byte[] rawBytes, bool is32Bit, int controlSet)
    {
        ParseErrors = new List<string>();
        ControlSet = controlSet;
        EntryCount = (int)Bytes.U32(rawBytes, 4, "Number of entries");

        Is32Bit = EntryTable.Is32Bit(rawBytes, 128, EntryCount, is32Bit, EntryTable.Win7x86, EntryTable.Win7x64);
        Entries = EntryTable.Read(rawBytes, 128, EntryCount, Is32Bit ? EntryTable.Win7x86 : EntryTable.Win7x64, controlSet, true, ParseErrors);
    }

    /// <summary>Whether the entries were read with the 32 bit layout</summary>
    public bool Is32Bit { get; }

    public List<CacheEntry> Entries { get; }

    /// <summary>Always empty: entries in this format can't be recognized in slack</summary>
    public List<CacheEntry> SlackEntries { get; } = new();
    public int EntryCount { get; }
    public int ControlSet { get; }
    public List<string> ParseErrors { get; }
}

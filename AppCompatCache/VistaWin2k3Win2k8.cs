using System.Collections.Generic;

namespace AppCompatCache;

/// <summary>
///     Windows 2003, Vista and 2008 (signature 0xBADC0FFE): an 8 byte header with the number of entries at 4, then 24
///     byte (32 bit) or 32 byte (64 bit) entries. On 64 bit Windows, insertion flag 0x2 means executed (Windows 2003 has
///     the file size there instead)
/// </summary>
public class VistaWin2k3Win2k8 : IAppCompatCache
{
    public VistaWin2k3Win2k8(byte[] rawBytes, bool is32Bit, int controlSet)
    {
        ParseErrors = new List<string>();
        ControlSet = controlSet;
        EntryCount = (int)Bytes.U32(rawBytes, 4, "Number of entries");

        Is32Bit = EntryTable.Is32Bit(rawBytes, 8, EntryCount, is32Bit, EntryTable.Vista32, EntryTable.Vista64);
        Entries = EntryTable.Read(rawBytes, 8, EntryCount, Is32Bit ? EntryTable.Vista32 : EntryTable.Vista64, controlSet, Is32Bit == false, ParseErrors);
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

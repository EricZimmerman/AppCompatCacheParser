using System;
using System.Collections.Generic;
using Serilog;

namespace AppCompatCache;

/// <summary>
///     Windows XP (signature 0xDEADBEEF; 64 bit XP uses the Windows 2003 format): a 400 byte header (number of slots,
///     number of entries in the LRU array, the LRU array of slot numbers, most recently used first), then 96 slots of
///     552 bytes: a 528 byte UTF-16 path, last modified FILETIME, file size, last update FILETIME. Entries are returned
///     in LRU order, like the other versions' (most recent first)
/// </summary>
public class WindowsXP : IAppCompatCache
{
    private const int HeaderSize = 400;
    private const int SlotSize = 552;

    public WindowsXP(byte[] rawBytes, bool is32Bit, int controlSet)
    {
        Entries = new List<CacheEntry>();
        ParseErrors = new List<string>();
        ControlSet = controlSet;

        var slots = (int)Bytes.U32(rawBytes, 4, "Number of slots");
        EntryCount = (int)Bytes.U32(rawBytes, 8, "Number of LRU entries");

        Log.Debug("**** Slots: {Slots}, entries in the LRU array: {EntryCount}", slots, EntryCount);

        //the LRU array is in the header, after these 16 bytes
        var lruMax = (HeaderSize - 16) / 4;
        if (EntryCount > lruMax)
        {
            ParseErrors.Add($"The LRU array has {EntryCount} entries, but there is room for {lruMax}");
            EntryCount = lruMax;
        }

        var seen = new HashSet<uint>();
        for (var position = 0; position < EntryCount; position++)
        {
            var slot = Bytes.U32(rawBytes, 16 + position * 4, "LRU array");
            var at = HeaderSize + (long)slot * SlotSize;
            try
            {
                if (seen.Add(slot) == false)
                {
                    throw new AppCompatCacheParseException($"Slot {slot} is in the LRU array more than once", 16 + position * 4, "LRU array");
                }

                Bytes.Need(rawBytes, at, SlotSize, $"Slot {slot}");

                var path = Bytes.Utf16(rawBytes, at, 528, "Path");
                var end = path.IndexOf('\0');
                var ce = new CacheEntry
                {
                    PathSize = 528,
                    Path = Bytes.CleanPath((end >= 0 ? path.Substring(0, end) : path).Trim()),
                    LastModifiedTimeUTC = Bytes.FileTime(Bytes.I64(rawBytes, at + 528, "Last modified time")),
                    DataSize = 0,
                    LastUpdateTimeUTC = Bytes.FileTime(Bytes.I64(rawBytes, at + 544, "Last update time")),
                    CacheEntryPosition = position,
                    ControlSet = controlSet,
                    Executed = AppCompatCache.Execute.NA
                };

                Entries.Add(ce);
            }
            catch (Exception ex)
            {
                Log.Error("Error parsing cache entry. Position: {Position} Slot: {Slot}, Error: {Message}", position, slot, ex.Message);
                ParseErrors.Add($"Entry {position} (slot {slot}): {ex.Message}");
            }
        }
    }

    public List<CacheEntry> Entries { get; }

    /// <summary>Always empty: entries in this format can't be recognized in slack</summary>
    public List<CacheEntry> SlackEntries { get; } = new();
    public int EntryCount { get; }
    public int ControlSet { get; }
    public List<string> ParseErrors { get; }
}

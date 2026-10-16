using System;
using System.Collections.Generic;
using Serilog;

namespace AppCompatCache;

/// <summary>
///     Windows 10 and 11: a header (0x30 bytes, or 0x34 from the Creators Update on), then "10ts" entries: signature,
///     unknown, entry size (after these 12 bytes), path size, UTF-16 path, last modified FILETIME, data size, data
/// </summary>
public class Windows10 : IAppCompatCache
{
    /// <summary>The data tag Windows sets when the file was executed (see <see cref="DataTags" />)</summary>
    public const uint ExecutedTag = 0x100;

    public int ExpectedEntries { get; }

    public Windows10(byte[] rawBytes, int controlSet)
    {
        Entries = new List<CacheEntry>();
        ParseErrors = new List<string>();
        ControlSet = controlSet;
        EntryCount = -1;

        var offsetToRecords = (int)Bytes.U32(rawBytes, 0, "Header size");

        ExpectedEntries = offsetToRecords == 0x34
            ? (int)Bytes.U32(rawBytes, 0x28, "Number of entries")
            : (int)Bytes.U32(rawBytes, 0x24, "Number of entries");

        var index = (long)offsetToRecords;
        var position = 0;

        while (index + 12 <= rawBytes.Length)
        {
            if (Bytes.Ascii(rawBytes, index, 4) != "10ts")
            {
                break;
            }

            try
            {
                Entries.Add(ReadEntry(rawBytes, index, position, controlSet, out var next));
                position += 1;
                index = next;
            }
            catch (Exception ex)
            {
                Log.Error("Error parsing cache entry. Position: {Position} Index: 0x{Index:X}, Error: {Message}", position, index, ex.Message);
                ParseErrors.Add($"Entry {position} at offset 0x{index:X}: {ex.Message}");

                //entry sizes are what get us to the next one, so the rest can't be found
                break;
            }
        }
    }

    /// <summary>A "10ts" entry at <paramref name="index" />; <paramref name="next" /> is where the next one starts</summary>
    internal static CacheEntry ReadEntry(byte[] rawBytes, long index, int position, int controlSet, out long next)
    {
        var entrySize = Bytes.U32(rawBytes, index + 8, "Entry size");
        next = index + 12 + entrySize;
        Bytes.Need(rawBytes, index + 12, entrySize, "Entry");

        var ce = new CacheEntry
        {
            Signature = "10ts",
            CacheEntryPosition = position,
            ControlSet = controlSet
        };

        var i = index + 12;
        ce.PathSize = Bytes.U16(rawBytes, i, "Path size");
        i += 2;
        if (i + ce.PathSize > next)
        {
            throw new AppCompatCacheParseException($"Path size 0x{ce.PathSize:X} is past the end of the entry", i - 2, "Path size");
        }

        ce.Path = Bytes.CleanPath(Bytes.Utf16(rawBytes, i, ce.PathSize, "Path"));
        i += ce.PathSize;

        ce.LastModifiedTimeUTC = Bytes.FileTime(Bytes.I64(rawBytes, i, "Last modified time"));
        i += 8;

        ce.DataSize = (int)Bytes.U32(rawBytes, i, "Data size");
        i += 4;
        if (ce.DataSize < 0 || i + ce.DataSize > next)
        {
            throw new AppCompatCacheParseException($"Data size 0x{ce.DataSize:X} is past the end of the entry", i - 4, "Data size");
        }

        ce.Data = Bytes.Slice(rawBytes, i, ce.DataSize, "Data");

        ce.Executed = HasTag(ce.Data, ExecutedTag) ? AppCompatCache.Execute.Yes : AppCompatCache.Execute.No;

        return ce;
    }

    public List<CacheEntry> Entries { get; }

    /// <summary>Entries found in the value's slack (see <see cref="Slack" />)</summary>
    public List<CacheEntry> SlackEntries { get; } = new();

    public int EntryCount { get; }
    public int ControlSet { get; }
    public List<string> ParseErrors { get; }

    /// <summary>
    ///     The data of a Windows 10/11 entry: 4 byte aligned records of a tag, a size and that many bytes of value.
    ///     Tag 0x100 (value 1) is there when the file was executed; 0x800 and 0x2000 hold the PE machine type (0x8664,
    ///     0x14C). Stops at the first record that doesn't fit
    /// </summary>
    public static IEnumerable<(uint Tag, byte[] Value)> DataTags(byte[] data)
    {
        if (data == null)
        {
            yield break;
        }

        var i = 0L;
        while (i + 8 <= data.Length)
        {
            var tag = BitConverter.ToUInt32(data, (int)i);
            var size = BitConverter.ToUInt32(data, (int)i + 4);
            if (i + 8 + size > data.Length)
            {
                yield break;
            }

            yield return (tag, Bytes.Slice(data, i + 8, size, "Data tag"));
            i += 8 + ((size + 3) & ~3L);
        }
    }

    private static bool HasTag(byte[] data, uint tag)
    {
        foreach (var t in DataTags(data))
        {
            if (t.Tag == tag)
            {
                return true;
            }
        }

        return false;
    }
}

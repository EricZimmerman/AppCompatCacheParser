using System;
using System.Collections.Generic;
using Serilog;

namespace AppCompatCache;

/// <summary>
///     Windows 8.0 ("00ts" entries) and 8.1 ("10ts"): a 128 byte header, then entries: signature, unknown, entry size
///     (after these 12 bytes), path size, UTF-16 path, package data size and package data, insertion flags, shim flags,
///     last modified FILETIME, data size, data
/// </summary>
public class Windows8x : IAppCompatCache
{
    public Windows8x(byte[] rawBytes, AppCompatCache.OperatingSystemVersion os, int controlSet)
    {
        Entries = new List<CacheEntry>();
        ParseErrors = new List<string>();
        ControlSet = controlSet;
        EntryCount = -1;

        var signature = os == AppCompatCache.OperatingSystemVersion.Windows81_Windows2012R2 ? "10ts" : "00ts";

        var index = 128L;
        var position = 0;

        while (index + 12 <= rawBytes.Length)
        {
            if (Bytes.Ascii(rawBytes, index, 4) != signature)
            {
                break;
            }

            try
            {
                Entries.Add(ReadEntry(rawBytes, index, signature, position, controlSet, out var next));
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

    /// <summary>An entry with <paramref name="signature" /> at <paramref name="index" />; <paramref name="next" /> is where the next one starts</summary>
    internal static CacheEntry ReadEntry(byte[] rawBytes, long index, string signature, int position, int controlSet, out long next)
    {
        var entrySize = Bytes.U32(rawBytes, index + 8, "Entry size");
        next = index + 12 + entrySize;
        Bytes.Need(rawBytes, index + 12, entrySize, "Entry");

        var ce = new CacheEntry
        {
            Signature = signature,
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

        var packageLen = Bytes.U16(rawBytes, i, "Package data size");
        i += 2 + packageLen;

        ce.InsertFlags = (AppCompatCache.InsertFlag)Bytes.U32(rawBytes, i, "Insertion flags");
        i += 4;

        //shim flags
        i += 4;

        ce.LastModifiedTimeUTC = Bytes.FileTime(Bytes.I64(rawBytes, i, "Last modified time"));
        i += 8;

        ce.DataSize = (int)Bytes.U32(rawBytes, i, "Data size");
        i += 4;
        if (ce.DataSize < 0 || i + ce.DataSize > next)
        {
            throw new AppCompatCacheParseException($"Data size 0x{ce.DataSize:X} is past the end of the entry", i - 4, "Data size");
        }

        ce.Data = Bytes.Slice(rawBytes, i, ce.DataSize, "Data");

        ce.Executed = (ce.InsertFlags & AppCompatCache.InsertFlag.Executed) == AppCompatCache.InsertFlag.Executed
            ? AppCompatCache.Execute.Yes
            : AppCompatCache.Execute.No;

        return ce;
    }

    public List<CacheEntry> Entries { get; }

    /// <summary>Entries found in the value's slack (see <see cref="Slack" />)</summary>
    public List<CacheEntry> SlackEntries { get; } = new();

    public int EntryCount { get; }
    public int ControlSet { get; }
    public List<string> ParseErrors { get; }
}

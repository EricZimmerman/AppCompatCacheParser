using System;
using System.Collections.Generic;
using Serilog;

namespace AppCompatCache;

/// <summary>
///     The Windows 2003/Vista/2008 and Windows 7/2008 R2 formats: a header with the number of entries, then fixed size
///     entries (path size, maximum path size, path offset, last modified FILETIME, ...) whose paths are elsewhere in the
///     value. The layout differs between 32 and 64 bit Windows, so it is checked against the data
/// </summary>
internal static class EntryTable
{
    public sealed class Layout
    {
        public int EntrySize;
        public int PathOffsetAt;
        public bool PathOffset64;
        public int TimeAt;
        public int FlagsAt;

        /// <summary>Data size and offset (Windows 7), or -1</summary>
        public int DataAt = -1;
    }

    public static readonly Layout Vista32 = new() { EntrySize = 24, PathOffsetAt = 4, TimeAt = 8, FlagsAt = 16 };
    public static readonly Layout Vista64 = new() { EntrySize = 32, PathOffsetAt = 8, PathOffset64 = true, TimeAt = 16, FlagsAt = 24 };
    public static readonly Layout Win7x86 = new() { EntrySize = 32, PathOffsetAt = 4, TimeAt = 8, FlagsAt = 16, DataAt = 24 };
    public static readonly Layout Win7x64 = new() { EntrySize = 48, PathOffsetAt = 8, PathOffset64 = true, TimeAt = 16, FlagsAt = 24, DataAt = 32 };

    /// <summary>
    ///     The layout for the hive's architecture when the first entry makes sense with it, else the other one when that
    ///     one does (the architecture value can be missing or wrong), else the hive's
    /// </summary>
    public static bool Is32Bit(byte[] b, int tableStart, int count, bool is32BitHint, Layout layout32, Layout layout64)
    {
        var hinted = is32BitHint ? layout32 : layout64;
        var other = is32BitHint ? layout64 : layout32;
        if (Plausible(b, tableStart, count, hinted) || Plausible(b, tableStart, count, other) == false)
        {
            return is32BitHint;
        }

        Log.Warning("The AppCompatCache entries are laid out as on {Bits} bit Windows, which is how they will be read", is32BitHint ? 64 : 32);
        return !is32BitHint;
    }

    private static bool Plausible(byte[] b, int tableStart, int count, Layout l)
    {
        if (count == 0 || Bytes.Fits(b, tableStart, l.EntrySize) == false)
        {
            return false;
        }

        var size = BitConverter.ToUInt16(b, tableStart);
        var max = BitConverter.ToUInt16(b, tableStart + 2);
        var offset = l.PathOffset64 ? BitConverter.ToInt64(b, tableStart + l.PathOffsetAt) : BitConverter.ToUInt32(b, tableStart + l.PathOffsetAt);
        var tableEnd = tableStart + (long)count * l.EntrySize;
        return size % 2 == 0 && max >= size && offset >= tableEnd && Bytes.Fits(b, offset, size);
    }

    /// <summary>Entries 0 to count - 1; one that can't be read goes to <paramref name="errors" /> and the rest are still read</summary>
    public static List<CacheEntry> Read(byte[] b, int tableStart, int count, Layout l, int controlSet, bool flagsMeanExecuted, List<string> errors)
    {
        var entries = new List<CacheEntry>();
        for (var n = 0; n < count; n++)
        {
            var at = tableStart + (long)n * l.EntrySize;
            if (Bytes.Fits(b, at, l.EntrySize) == false)
            {
                errors.Add($"Entries {n} to {count - 1} are past the end of the data (0x{b.Length:X} bytes)");
                break;
            }

            try
            {
                var ce = new CacheEntry { CacheEntryPosition = n, ControlSet = controlSet };

                ce.PathSize = Bytes.U16(b, at, "Path size");
                var offset = l.PathOffset64 ? Bytes.I64(b, at + l.PathOffsetAt, "Path offset") : Bytes.U32(b, at + l.PathOffsetAt, "Path offset");
                ce.Path = Bytes.CleanPath(Bytes.Utf16(b, offset, ce.PathSize, "Path"));

                ce.LastModifiedTimeUTC = Bytes.FileTime(Bytes.I64(b, at + l.TimeAt, "Last modified time"));
                ce.InsertFlags = (AppCompatCache.InsertFlag)Bytes.U32(b, at + l.FlagsAt, "Insertion flags");

                if (l.DataAt >= 0)
                {
                    var dataSize = l.PathOffset64 ? Bytes.I64(b, at + l.DataAt, "Data size") : Bytes.U32(b, at + l.DataAt, "Data size");
                    var dataOffset = l.PathOffset64 ? Bytes.I64(b, at + l.DataAt + 8, "Data offset") : Bytes.U32(b, at + l.DataAt + 4, "Data offset");
                    if (dataSize > 0 && Bytes.Fits(b, dataOffset, dataSize))
                    {
                        ce.DataSize = (int)dataSize;
                        ce.Data = Bytes.Slice(b, dataOffset, dataSize, "Data");
                    }
                }

                if (flagsMeanExecuted)
                {
                    ce.Executed = (ce.InsertFlags & AppCompatCache.InsertFlag.Executed) == AppCompatCache.InsertFlag.Executed
                        ? AppCompatCache.Execute.Yes
                        : AppCompatCache.Execute.No;
                }
                else
                {
                    ce.Executed = AppCompatCache.Execute.NA;
                }

                entries.Add(ce);
            }
            catch (Exception ex)
            {
                Log.Error("Error parsing cache entry. Position: {Position} Index: 0x{Index:X}, Error: {Message}", n, at, ex.Message);
                errors.Add($"Entry {n} at offset 0x{at:X}: {ex.Message}");
            }
        }

        return entries;
    }
}

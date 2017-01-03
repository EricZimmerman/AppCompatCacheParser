using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NLog;

namespace AppCompatCache
{
    public class WindowsXP : IAppCompatCache
    {
        public WindowsXP(byte[] rawBytes, bool is32Bit, int controlSet)
        {
            Entries = new List<CacheEntry>();

            var index = 4;
            ControlSet = controlSet;

            EntryCount = BitConverter.ToInt32(rawBytes, index);
            index += 4;

            var lruArrauEntries = BitConverter.ToUInt32(rawBytes, index);
            index += 4;

            index = 400;

            var position = 0;

            var log1 = LogManager.GetCurrentClassLogger();

            log1.Debug($@"**** 32 bit system?: {is32Bit}");

            log1.Debug($@"**** EntryCount found: {EntryCount}");

            if (EntryCount == 0)
            {
                return; 
            }

            if (is32Bit)
            {
                while (index < rawBytes.Length)
                {
                    try
                    {
                        log1.Debug($@"**** At index position: {index}");

                        var ce = new CacheEntry {PathSize = 528};


                        ce.Path = Encoding.Unicode.GetString(rawBytes, index, ce.PathSize).Split('\0').First().Replace('\0', ' ').Trim().Replace(@"\??\", "");
                        index += 528;

                        ce.LastModifiedTimeUTC =
                            DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
                        index += 8;

                        var fileSize = BitConverter.ToUInt64(rawBytes, index);
                        index += 8;

                        //                        ce.LastModifiedTimeUTC =
                        //                            DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
                        //this is last update time, its not reported yet
                        index += 8;

                        if (ce.LastModifiedTimeUTC.Year == 1601)
                        {
                            break;
                        }

                        ce.CacheEntryPosition = position;
                        ce.ControlSet = controlSet;

                        ce.Executed = AppCompatCache.Execute.NA;

                        log1.Debug($@"**** Adding cache entry for '{ce.Path}' to Entries");

                        Entries.Add(ce);
                        position += 1;

                        if (Entries.Count == EntryCount)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        var _log = LogManager.GetCurrentClassLogger();
                        _log.Error(
                            $"Error parsing cache entry. Position: {position} Index: {index}, Error: {ex.Message} ");
                        //TODO Report this
                        if (Entries.Count < EntryCount)
                        {
                            throw;
                        }
                        //take what we can get
                        break;
                    }
                }
            }
            else
            {
                throw new Exception(
                    "64 bit XP support not available. send the hive to saericzimmerman@gmail.com so support can be added");
//                while (index < rawBytes.Length)
//                {
//                    try
//                    {
//                        var ce = new CacheEntry {PathSize = BitConverter.ToUInt16(rawBytes, index)};
//
//                        index += 2;
//
//                        var maxPathSize = BitConverter.ToUInt16(rawBytes, index);
//                        index += 2;
//
//
//                        var pathOffset = BitConverter.ToInt32(rawBytes, index);
//                        index += 4;
//
//                        ce.LastModifiedTimeUTC =
//                            DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
//                        index += 8;
//
//                        // skip 4 unknown (insertion flags?)
//                        index += 4;
//
//                        // skip 4 unknown (shim flags?)
//                        index += 4;
//
//                        var ceDataSize = BitConverter.ToUInt32(rawBytes, index);
//                        index += 4;
//
//                        var dataOffset = BitConverter.ToUInt32(rawBytes, index);
//                        index += 4;
//
//                        ce.Path = Encoding.Unicode.GetString(rawBytes, pathOffset, ce.PathSize);
//
//                        if (ce.LastModifiedTimeUTC.Year == 1601)
//                        {
//                            break;
//                        }
//
//                        ce.CacheEntryPosition = position;
//                        Entries.Add(ce);
//                        position += 1;
//
//
//                        if (Entries.Count == EntryCount)
//                        {
//                            break;
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        //TODO Report this
//                        Debug.WriteLine(ex.Message);
//                        //take what we can get
//                        break;
//                    }
//                }
            }
        }

        public List<CacheEntry> Entries { get; }
        public int EntryCount { get; }
        public int ControlSet { get; }
    }
}
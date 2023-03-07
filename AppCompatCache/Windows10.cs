using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Serilog;

namespace AppCompatCache;

public class Windows10 : IAppCompatCache
{
    public int ExpectedEntries { get; }

    public Windows10(byte[] rawBytes, int controlSet)
    {
        Entries = new List<CacheEntry>();

        ExpectedEntries = 0;

        var offsetToRecords = BitConverter.ToInt32(rawBytes, 0);

        ExpectedEntries = BitConverter.ToInt32(rawBytes, 0x24);

        if (offsetToRecords == 0x34)
        {
            ExpectedEntries = BitConverter.ToInt32(rawBytes, 0x28);
        }

        var index = offsetToRecords;
        ControlSet = controlSet;

        EntryCount = -1;

        var position = 0;

        while (index < rawBytes.Length)
        {
            try
            {
                var ce = new CacheEntry
                {
                    Signature = Encoding.ASCII.GetString(rawBytes, index, 4)
                };

                if (ce.Signature != "10ts")
                {
                    break;
                }

                index += 4;

                // skip 4 unknown
                index += 4;

                var ceDataSize = BitConverter.ToUInt32(rawBytes, index);
                index += 4;

                ce.PathSize = BitConverter.ToUInt16(rawBytes, index);
                index += 2;
                ce.Path = Encoding.Unicode.GetString(rawBytes, index, ce.PathSize).Replace(@"\??\", "");
                index += ce.PathSize;

                ce.LastModifiedTimeUTC =
                    DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();

                if (ce.LastModifiedTimeUTC.Value.Year == 1601)
                {
                    ce.LastModifiedTimeUTC = null;
                }

                index += 8;

                ce.DataSize = BitConverter.ToInt32(rawBytes, index);
                index += 4;

                ce.Data = rawBytes.Skip(index).Take(ce.DataSize).ToArray();
                index += ce.DataSize;

                //if the last 4 bytes of Data is 1, it indicates execution
                var exec= BitConverter.ToInt32(ce.Data, ce.Data.Length - 4) == 1;

                ce.Executed = AppCompatCache.Execute.No;
                if (exec)
                {
                    ce.Executed = AppCompatCache.Execute.Yes; 
                }

                ce.ControlSet = controlSet;
                ce.CacheEntryPosition = position;

                Entries.Add(ce);
                position += 1;
            }
            catch (Exception ex)
            {
                Log.Error(ex,
                    "Error parsing cache entry. Position: {Position} Index: {Index}, Error: {Message} ",position,index,ex.Message);
                    
                //TODO Report this
                //take what we can get
                break;
            }
        }
    }

    public List<CacheEntry> Entries { get; }
    public int EntryCount { get; }
    public int ControlSet { get; }
}
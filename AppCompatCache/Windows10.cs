using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace AppCompatCache
{
    public class Windows10 : IAppCompatCache
    {
        public Windows10(byte[] rawBytes, int controlSet)
        {
            Entries = new List<CacheEntry>();

            var index = 48;
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
                    index += 8;

                    ce.DataSize = BitConverter.ToInt32(rawBytes, index);
                    index += 4;

                    ce.Data = rawBytes.Skip(index).Take(ce.DataSize).ToArray();
                    index += ce.DataSize;

                    ce.Executed = AppCompatCache.Execute.Unknown;

                    ce.ControlSet = controlSet;
                    ce.CacheEntryPosition = position;

                    Entries.Add(ce);
                    position += 1;
                }
                catch (Exception ex)
                {
                    var _log = LogManager.GetCurrentClassLogger();
                    _log.Error($"Error parsing cache entry. Position: {position} Index: {index}, Error: {ex.Message} ");
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
}
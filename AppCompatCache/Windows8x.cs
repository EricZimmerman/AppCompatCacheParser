using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppCompatCache
{
    public class Windows8x : IAppCompatCache
    {
        public Windows8x(byte[] rawBytes, AppCompatCache.OperatingSystemVersion os)
        {
            Entries = new List<CacheEntry>();

            var index = 128;

            var signature = "00ts";

            if (os == AppCompatCache.OperatingSystemVersion.Windows81_Windows2012R2)
            {
                signature = "10ts";
            }

            var position = 0;

            while (index <= rawBytes.Length)
            {
                try
                {
                    var ce = new CacheEntry
                    {
                        Signature = Encoding.ASCII.GetString(rawBytes, index, 4)
                    };

                    if (ce.Signature != signature)
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

                    ce.Path = Encoding.Unicode.GetString(rawBytes, index, ce.PathSize);
                    index += ce.PathSize;

                    // skip 4 unknown (insertion flags?)
                    index += 4;

                    // skip 4 unknown (shim flags?)
                    index += 4;

                    // skip 2 unknown
                    index += 2;

                    ce.LastModifiedTimeUTC =
                        DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();

                    index += 8;

                    ce.DataSize = BitConverter.ToInt32(rawBytes, index);
                    index += 4;

                    ce.Data = rawBytes.Skip(index).Take(ce.DataSize).ToArray();
                    index += ce.DataSize;

                    ce.CacheEntryPosition = position;

                    Entries.Add(ce);
                    position += 1;
                }
                catch (Exception ex)
                {
                    //TODO report this
                    //take what we can get
                    break;
                }
            }
        }

        public List<CacheEntry> Entries { get; }
    }
}
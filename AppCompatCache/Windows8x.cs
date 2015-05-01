using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppCompatCache
{
    public class Windows8x : IAppCompatCache
    {
        public Windows8x(byte[] rawBytes, string signature)
        {
            Entries = new List<CacheEntry>();

            var index = 128;

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

                    ce.LastModifiedTime = DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index));

                    index += 8;

                    ce.DataSize = BitConverter.ToInt32(rawBytes, index);
                    index += 4;

                    ce.Data = rawBytes.Skip(index).Take(ce.DataSize).ToArray();
                    index += ce.DataSize;

                    Entries.Add(ce);
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
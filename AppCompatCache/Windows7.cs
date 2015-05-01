using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AppCompatCache
{
    public class Windows7 : IAppCompatCache
    {
        public Windows7(byte[] rawBytes, bool is32Bit)
        {
            Entries = new List<CacheEntry>();

            var index = 4;

            var cacheItems = BitConverter.ToUInt32(rawBytes, index);

            index = 128;

            if ((is32Bit))
            {
                while (index <= rawBytes.Length)
                {
                    try
                    {
                        var ce = new CacheEntry();

                        ce.PathSize = BitConverter.ToUInt16(rawBytes, index);
                        index += 2;

                        var maxPathSize = BitConverter.ToUInt16(rawBytes, index);
                        index += 2;


                        var pathOffset = BitConverter.ToInt32(rawBytes, index);
                        index += 4;

                        ce.LastModifiedTime = DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index));
                        index += 8;

                        // skip 4 unknown (insertion flags?)
                        index += 4;

                        // skip 4 unknown (shim flags?)
                        index += 4;

                        var ceDataSize = BitConverter.ToUInt32(rawBytes, index);
                        index += 4;

                        var dataOffset = BitConverter.ToUInt32(rawBytes, index);
                        index += 4;

                        ce.Path = Encoding.Unicode.GetString(rawBytes, pathOffset, ce.PathSize);

                        Entries.Add(ce);

                        if (Entries.Count == cacheItems)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        //TODO Report this
                        Debug.WriteLine(ex.Message);
                        //take what we can get
                        break;
                    }
                }
            }
            else
            {
                while (index <= rawBytes.Length)
                {
                    try
                    {
                        var ce = new CacheEntry();

                        ce.PathSize = BitConverter.ToUInt16(rawBytes, index);
                        index += 2;

                        var maxPathSize = BitConverter.ToUInt16(rawBytes, index);
                        index += 2;

                        // skip 4 unknown (padding)
                        index += 4;

                        var pathOffset = BitConverter.ToInt64(rawBytes, index);
                        index += 8;

                        ce.LastModifiedTime = DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index));
                        index += 8;

                        // skip 4 unknown (insertion flags?)
                        index += 4;

                        // skip 4 unknown (shim flags?)
                        index += 4;

                        var ceDataSize = BitConverter.ToUInt64(rawBytes, index);
                        index += 8;

                        var dataOffset = BitConverter.ToUInt64(rawBytes, index);
                        index += 8;

                        ce.Path = Encoding.Unicode.GetString(rawBytes, (int) pathOffset, ce.PathSize);

                        Entries.Add(ce);

                        if (Entries.Count == cacheItems)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        //TODO Report this
                        //take what we can get
                        break;
                    }
                }
            }
        }

        public List<CacheEntry> Entries { get;  }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AppCompatCache
{
    public class WindowsXP : IAppCompatCache
    {
        public WindowsXP(byte[] rawBytes, bool is32Bit)
        {
            Entries = new List<CacheEntry>();

            var index = 4;

            var cacheItems = BitConverter.ToUInt32(rawBytes, index);
            index += 4;

            var lruArrauEntries = BitConverter.ToUInt32(rawBytes, index);
            index += 4;

            index = 400;

            var position = 0;

            if ((is32Bit))
            {
                while (index <= rawBytes.Length)
                {
                    try
                    {
                        var ce = new CacheEntry();

                        ce.PathSize = 528;
                        // index += 2;

                        //                        var maxPathSize = BitConverter.ToUInt16(rawBytes, index);
                        //                        index += 2;

                        ce.Path = Encoding.Unicode.GetString(rawBytes, index, ce.PathSize).Replace('\0', ' ').Trim();
                        index += 528;

                        ce.LastModifiedTimeUTC =
                            DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
                        index += 8;

                        var fileSize = BitConverter.ToUInt64(rawBytes, index);
                        index += 8;

                        //                        ce.LastModifiedTimeUTC =
                        //                            DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
                        //this is last update time
                        index += 8;

                        if (ce.LastModifiedTimeUTC.Year == 1601)
                        {
                            break;
                        }

                        ce.CacheEntryPosition = position;
                        Entries.Add(ce);
                        position += 1;

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
            else
            {

                throw new Exception("64 bit XP support not available. send the hive to saericzimmerman@gmail.com so support can be added");
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

                        ce.LastModifiedTimeUTC =
                            DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
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

                        if (ce.LastModifiedTimeUTC.Year == 1601)
                        {
                            break;
                        }

                        ce.CacheEntryPosition = position;
                        Entries.Add(ce);
                        position += 1;



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
        }

        public List<CacheEntry> Entries { get; }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Serilog;

namespace AppCompatCache;

public class VistaWin2k3Win2k8 : IAppCompatCache
{
    public VistaWin2k3Win2k8(byte[] rawBytes, bool is32Bit, int controlSet)
    {
        Entries = new List<CacheEntry>();

        var index = 4;
        ControlSet = controlSet;

        EntryCount = BitConverter.ToInt32(rawBytes, index);

        index = 8;

        var position = 0;

        if (EntryCount == 0)
        {
            return; ;
        }

        if (is32Bit)
        {
            while (index < rawBytes.Length)
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

                    if (ce.LastModifiedTimeUTC.Value.Year == 1601)
                    {
                        ce.LastModifiedTimeUTC = null;
                    }

                    index += 8;

                    // skip 4 unknown (insertion flags?)
                    ce.InsertFlags = (AppCompatCache.InsertFlag)BitConverter.ToInt32(rawBytes, index);
                    index += 4;

                    // skip 4 unknown (shim flags?)
                    index += 4;

                    ce.Path = Encoding.Unicode.GetString(rawBytes, pathOffset, ce.PathSize).Replace(@"\??\", "");

//                        if ((ce.InsertFlags & AppCompatCache.InsertFlag.Executed) == AppCompatCache.InsertFlag.Executed)
//                        {
//                            ce.Executed = AppCompatCache.Execute.Yes;
//                        }
//                        else
//                        {
//                            ce.Executed = AppCompatCache.Execute.No;
//                        }

                    ce.Executed = AppCompatCache.Execute.NA;

                    ce.CacheEntryPosition = position;
                    ce.ControlSet = controlSet;
                    Entries.Add(ce);
                    position += 1;

                    if (Entries.Count == EntryCount)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                        
                    Log.Error(ex,"Error parsing cache entry. Position: {Position} Index: {Index}, Error: {Message} ",position,index,ex.Message);

                    if (Entries.Count < EntryCount)
                    {
                        throw;
                    }

                    //TODO Report this
                    Debug.WriteLine(ex.Message);
                    //take what we can get
                    break;
                }
            }
        }
        else
        {
                
            while (index < rawBytes.Length)
            {
                try
                {
                    var ce1 = new CacheEntry();

                    ce1.PathSize = BitConverter.ToUInt16(rawBytes, index);
                    index += 2;

                    var maxPathSize = BitConverter.ToUInt16(rawBytes, index);
                    index += 2;

                    // skip 4 unknown (padding)
                    index += 4;

                    var pathOffset = BitConverter.ToInt64(rawBytes, index);
                    index += 8;

                    ce1.LastModifiedTimeUTC =
                        DateTimeOffset.FromFileTime(BitConverter.ToInt64(rawBytes, index)).ToUniversalTime();
                    index += 8;

                    // skip 4 unknown (insertion flags?)
                    ce1.InsertFlags = (AppCompatCache.InsertFlag)BitConverter.ToInt32(rawBytes, index);
                    index += 4;

                    // skip 4 unknown (shim flags?)
                    index += 4;

//                        var ceDataSize = BitConverter.ToUInt64(rawBytes, index);
//                        index += 8;
//
//                        var dataOffset = BitConverter.ToUInt64(rawBytes, index);
//                        index += 8;

                    ce1.Path = Encoding.Unicode.GetString(rawBytes, (int)pathOffset, ce1.PathSize).Replace(@"\??\", "");

                    if ((ce1.InsertFlags & AppCompatCache.InsertFlag.Executed) == AppCompatCache.InsertFlag.Executed)
                    {
                        ce1.Executed = AppCompatCache.Execute.Yes;
                    }
                    else
                    {
                        ce1.Executed = AppCompatCache.Execute.No;
                    }

                    ce1.CacheEntryPosition = position;
                    ce1.ControlSet = controlSet;
                    Entries.Add(ce1);
                    position += 1;

                    if (Entries.Count == EntryCount)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex,"Error parsing cache entry. Position: {Position} Index: {Index}, Error: {Message} ",position,index,ex.Message);
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
    }

    public List<CacheEntry> Entries { get; }
    public int EntryCount { get; }
    public int ControlSet { get; }
}
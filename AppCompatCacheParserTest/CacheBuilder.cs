using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AppCompatCacheTest;

/// <summary>Builds AppCompatCache values in each format (layouts from libyal's winreg-kb)</summary>
public static class CacheBuilder
{
    public static readonly DateTime When = new(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    public static long Ft(DateTime t) => t.ToFileTimeUtc();

    /// <summary>Windows 10/11 data records: (tag, size, value), 4 byte aligned</summary>
    public static byte[] Tags(params (uint Tag, byte[] Value)[] tags)
    {
        var ms = new MemoryStream();
        foreach (var (tag, value) in tags)
        {
            ms.Write(BitConverter.GetBytes(tag));
            ms.Write(BitConverter.GetBytes(value.Length));
            ms.Write(value);
            while (ms.Length % 4 != 0)
            {
                ms.WriteByte(0);
            }
        }

        return ms.ToArray();
    }

    public static byte[] U32(uint v) => BitConverter.GetBytes(v);

    /// <summary>One "10ts" entry</summary>
    public static byte[] Win10Entry(string path, long ft, byte[] data)
    {
        var p = Encoding.Unicode.GetBytes(path);
        var body = new MemoryStream();
        body.Write(BitConverter.GetBytes((ushort)p.Length));
        body.Write(p);
        body.Write(BitConverter.GetBytes(ft));
        body.Write(BitConverter.GetBytes(data.Length));
        body.Write(data);

        var e = new MemoryStream();
        e.Write(Encoding.ASCII.GetBytes("10ts"));
        e.Write(U32(0x12345678));
        e.Write(BitConverter.GetBytes((uint)body.Length));
        e.Write(body.ToArray());
        return e.ToArray();
    }

    /// <summary>A Windows 10 (0x30 byte header) or Creators Update and later (0x34) value</summary>
    public static byte[] Win10(bool creators, params byte[][] entries)
    {
        var headerSize = creators ? 0x34 : 0x30;
        var header = new byte[headerSize];
        BitConverter.GetBytes(headerSize).CopyTo(header, 0);
        BitConverter.GetBytes(entries.Length).CopyTo(header, creators ? 0x28 : 0x24);
        return header.Concat(entries.SelectMany(t => t)).ToArray();
    }

    /// <summary>A Windows 8.0 ("00ts") or 8.1 ("10ts") value</summary>
    public static byte[] Win8(bool win81, params (string Path, long Ft, uint Flags, byte[] Package)[] entries)
    {
        var ms = new MemoryStream();
        var header = new byte[128];
        BitConverter.GetBytes(128).CopyTo(header, 0);
        ms.Write(header);
        foreach (var (path, ft, flags, package) in entries)
        {
            var p = Encoding.Unicode.GetBytes(path);
            var body = new MemoryStream();
            body.Write(BitConverter.GetBytes((ushort)p.Length));
            body.Write(p);
            body.Write(BitConverter.GetBytes((ushort)package.Length));
            body.Write(package);
            body.Write(U32(flags));
            body.Write(U32(0));
            body.Write(BitConverter.GetBytes(ft));
            body.Write(U32(4));
            body.Write(U32(0xAABBCCDD));

            ms.Write(Encoding.ASCII.GetBytes(win81 ? "10ts" : "00ts"));
            ms.Write(U32(0));
            ms.Write(BitConverter.GetBytes((uint)body.Length));
            ms.Write(body.ToArray());
        }

        return ms.ToArray();
    }

    /// <summary>A Windows 7 (header 128) or Vista/2003/2008 (header 8) value: an entry table, then the paths</summary>
    public static byte[] Table(bool win7, bool is32Bit, params (string Path, long Ft, uint Flags)[] entries)
    {
        var headerSize = win7 ? 128 : 8;
        var entrySize = win7 ? is32Bit ? 32 : 48 : is32Bit ? 24 : 32;
        var tableEnd = headerSize + entries.Length * entrySize;

        var b = new List<byte>(new byte[tableEnd]);
        var header = new byte[headerSize];
        BitConverter.GetBytes(win7 ? 0xBADC0FEE : 0xBADC0FFE).CopyTo(header, 0);
        BitConverter.GetBytes(entries.Length).CopyTo(header, 4);
        for (var i = 0; i < headerSize; i++)
        {
            b[i] = header[i];
        }

        for (var n = 0; n < entries.Length; n++)
        {
            var p = Encoding.Unicode.GetBytes(entries[n].Path);
            var pathOffset = b.Count;
            b.AddRange(p);
            b.AddRange(new byte[2]);

            var e = new byte[entrySize];
            BitConverter.GetBytes((ushort)p.Length).CopyTo(e, 0);
            BitConverter.GetBytes((ushort)(p.Length + 2)).CopyTo(e, 2);
            if (is32Bit)
            {
                BitConverter.GetBytes(pathOffset).CopyTo(e, 4);
                BitConverter.GetBytes(entries[n].Ft).CopyTo(e, 8);
                BitConverter.GetBytes(entries[n].Flags).CopyTo(e, 16);
            }
            else
            {
                BitConverter.GetBytes((long)pathOffset).CopyTo(e, 8);
                BitConverter.GetBytes(entries[n].Ft).CopyTo(e, 16);
                BitConverter.GetBytes(entries[n].Flags).CopyTo(e, 24);
            }

            for (var i = 0; i < entrySize; i++)
            {
                b[headerSize + n * entrySize + i] = e[i];
            }
        }

        return b.ToArray();
    }

    /// <summary>A Windows XP value: 96 slots (paths and times as given, in slot order) and the LRU array (most recent first)</summary>
    public static byte[] Xp(int[] lru, params (string Path, long Modified, long Updated)[] slots)
    {
        var b = new byte[400 + 96 * 552];
        BitConverter.GetBytes(0xDEADBEEF).CopyTo(b, 0);
        BitConverter.GetBytes(96).CopyTo(b, 4);
        BitConverter.GetBytes(lru.Length).CopyTo(b, 8);
        for (var i = 0; i < lru.Length; i++)
        {
            BitConverter.GetBytes(lru[i]).CopyTo(b, 16 + i * 4);
        }

        for (var i = 0; i < slots.Length; i++)
        {
            var at = 400 + i * 552;
            Encoding.Unicode.GetBytes(@"\??\" + slots[i].Path).CopyTo(b, at);
            BitConverter.GetBytes(slots[i].Modified).CopyTo(b, at + 528);
            BitConverter.GetBytes(1234L).CopyTo(b, at + 536);
            BitConverter.GetBytes(slots[i].Updated).CopyTo(b, at + 544);
        }

        return b;
    }
}

using System;
using System.Text;

namespace AppCompatCache;

/// <summary>Reads that check the bounds first: the value can be truncated or not what it claims to be</summary>
internal static class Bytes
{
    public static bool Fits(byte[] b, long offset, long length) => offset >= 0 && length >= 0 && offset + length <= b.Length;

    public static void Need(byte[] b, long offset, long length, string field)
    {
        if (Fits(b, offset, length) == false)
        {
            throw new AppCompatCacheParseException($"{field} (0x{length:X} bytes at 0x{offset:X}) is past the end of the data (0x{b.Length:X} bytes)", offset, field);
        }
    }

    public static ushort U16(byte[] b, long offset, string field)
    {
        Need(b, offset, 2, field);
        return BitConverter.ToUInt16(b, (int)offset);
    }

    public static uint U32(byte[] b, long offset, string field)
    {
        Need(b, offset, 4, field);
        return BitConverter.ToUInt32(b, (int)offset);
    }

    public static long I64(byte[] b, long offset, string field)
    {
        Need(b, offset, 8, field);
        return BitConverter.ToInt64(b, (int)offset);
    }

    public static ulong U64(byte[] b, long offset, string field)
    {
        Need(b, offset, 8, field);
        return BitConverter.ToUInt64(b, (int)offset);
    }

    public static string Utf16(byte[] b, long offset, int length, string field)
    {
        Need(b, offset, length, field);
        return Encoding.Unicode.GetString(b, (int)offset, length);
    }

    public static string Ascii(byte[] b, long offset, int length)
    {
        return Fits(b, offset, length) ? Encoding.ASCII.GetString(b, (int)offset, length) : string.Empty;
    }

    public static byte[] Slice(byte[] b, long offset, long length, string field)
    {
        Need(b, offset, length, field);
        var r = new byte[length];
        Buffer.BlockCopy(b, (int)offset, r, 0, (int)length);
        return r;
    }

    /// <summary>A FILETIME, or null for 0, 1601 (as the parsers always showed them) and values that are not valid times</summary>
    public static DateTimeOffset? FileTime(long value)
    {
        if (value <= 0 || value > DateTimeOffset.MaxValue.UtcTicks - 504911232000000000L)
        {
            return null;
        }

        var t = DateTimeOffset.FromFileTime(value).ToUniversalTime();
        return t.Year == 1601 ? null : t;
    }

    /// <summary>A path as the cache stores it, without the \??\ prefix</summary>
    public static string CleanPath(string path) => path.Replace(@"\??\", "");
}

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace AppCompatCache;

/// <summary>
///     Entries left in the slack of the value's cell (the space after the data when the cache shrank, or older data the
///     cell was reused over): entries that dropped out of the cache. Only the Windows 8 to 11 formats can be found
///     there, since their entries have a signature and say how big they are
/// </summary>
public static class Slack
{
    /// <summary>The entries in <paramref name="slack" />, numbered from <paramref name="firstPosition" /></summary>
    public static List<CacheEntry> Recover(byte[] slack, AppCompatCache.OperatingSystemVersion os, int controlSet, int firstPosition)
    {
        var entries = new List<CacheEntry>();
        if (slack == null || slack.Length < 12)
        {
            return entries;
        }

        string signature;
        switch (os)
        {
            case AppCompatCache.OperatingSystemVersion.Windows80_Windows2012:
                signature = "00ts";
                break;
            case AppCompatCache.OperatingSystemVersion.Windows81_Windows2012R2:
            case AppCompatCache.OperatingSystemVersion.Windows10:
            case AppCompatCache.OperatingSystemVersion.Windows10C_11:
                signature = "10ts";
                break;
            default:
                return entries;
        }

        var windows10 = os is AppCompatCache.OperatingSystemVersion.Windows10 or AppCompatCache.OperatingSystemVersion.Windows10C_11;

        var i = 0L;
        while (i + 12 <= slack.Length)
        {
            if (slack[i] != signature[0] || Bytes.Ascii(slack, i, 4) != signature)
            {
                i += 1;
                continue;
            }

            try
            {
                var position = firstPosition + entries.Count;
                var ce = windows10
                    ? Windows10.ReadEntry(slack, i, position, controlSet, out var next)
                    : Windows8x.ReadEntry(slack, i, signature, position, controlSet, out next);

                if (Plausible(ce))
                {
                    ce.FromSlack = true;
                    entries.Add(ce);
                    i = next;
                    continue;
                }
            }
            catch (AppCompatCacheParseException)
            {
                //a signature that isn't the start of a whole entry (or an entry cut off at the end)
            }

            i += 1;
        }

        Log.Debug("**** Recovered {Count} entries from slack", entries.Count);
        return entries;
    }

    /// <summary>A path without control characters, and a valid time (or none)</summary>
    private static bool Plausible(CacheEntry ce)
    {
        return string.IsNullOrEmpty(ce.Path) == false && ce.PathSize % 2 == 0 && ce.Path.All(c => c >= 0x20 || c == '\t');
    }
}

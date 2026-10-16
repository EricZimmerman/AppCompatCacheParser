using System;

namespace AppCompatCache;

public class CacheEntry
{
    public int CacheEntryPosition { get; set; }
    public int CacheEntrySize { get; set; }
    public byte[] Data { get; set; }
    public AppCompatCache.InsertFlag InsertFlags { get; set; }
    public AppCompatCache.Execute Executed { get; set; }
    public int DataSize { get; set; }
    public DateTimeOffset? LastModifiedTimeUTC { get; set; }

    /// <summary>Windows XP only: when the entry was last updated (the LRU array orders entries by it)</summary>
    public DateTimeOffset? LastUpdateTimeUTC { get; set; }
    public long LastModifiedFILETIMEUTC => LastModifiedTimeUTC.HasValue ? LastModifiedTimeUTC.Value.ToFileTime() : 0;
    public string Path { get; set; }
    public int PathSize { get; set; }
    public string Signature { get; set; }
    public int ControlSet { get; set; }

    public override string ToString()
    {
        return
            $"#{CacheEntryPosition} (Path size: {PathSize}), Path: {Path}, Last modified (UTC):{LastModifiedTimeUTC}";
    }

    public bool Duplicate { get; set; }

    /// <summary>Recovered from the slack of the value's cell (see <see cref="Slack" />), so no longer in the cache</summary>
    public bool FromSlack { get; set; }
    public string SourceFile { get; set; }

    public string GetKey()
    {
        return $"{LastModifiedFILETIMEUTC}{Path?.ToUpperInvariant()}";
    }
}
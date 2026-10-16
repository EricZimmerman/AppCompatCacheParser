using System;

namespace AppCompatCache;

/// <summary>An AppCompatCache value that can't be parsed at all (too small, or an unknown format)</summary>
public class AppCompatCacheParseException : Exception
{
    public AppCompatCacheParseException(string message, long offset, string field) : base(message)
    {
        Offset = offset;
        Field = field;
    }

    /// <summary>Where in the value the problem is</summary>
    public long Offset { get; }

    /// <summary>What was being read</summary>
    public string Field { get; }
}

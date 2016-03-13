using System;

namespace AppCompatCache
{
    public class CacheEntry
    {
        public int CacheEntryPosition { get; set; }
        public int CacheEntrySize { get; set; }
        public byte[] Data { get; set; }
        public int DataSize { get; set; }
        public DateTimeOffset LastModifiedTimeUTC { get; set; }
        public string Path { get; set; }
        public int PathSize { get; set; }
        public string Signature { get; set; }

        public override string ToString()
        {
            return
                $"#{CacheEntryPosition} (Path size: {PathSize}), Path: {Path}, Last modified (UTC):{LastModifiedTimeUTC}";
        }
    }
}
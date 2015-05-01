using System;

namespace AppCompatCache
{
    public class CacheEntry
    {
        public int CacheEntrySize { get; set; }
        public byte[] Data { get; set; }
        public int DataSize { get; set; }
        public DateTimeOffset LastModifiedTime { get; set; }
        public string Path { get; set; }
        public int PathSize { get; set; }
        public string Signature { get; set; }

        public override string ToString()
        {
            return $"(Path size: {PathSize}), Path: {Path}, Last modified:{LastModifiedTime}";
        }
    }
}
using System;

namespace AppCompatCache
{
    public class CacheEntry
    {
        public int CacheEntrySize;
        public byte[] Data;
        public int DataSize;
        public DateTimeOffset LastModifiedTime;
        public string Path;
        public int PathSize;
        public string Signature;
    }
}
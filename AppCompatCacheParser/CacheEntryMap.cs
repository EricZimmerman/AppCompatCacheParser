using System;
using System.Globalization;
using AppCompatCache;
using CsvHelper;

namespace AppCompatCacheParser;

/// <summary>
///     The CSV columns of <see cref="CacheEntry" /> (a library type, which has no CsvHelper attributes), in the order
///     AppCompatCacheParser has always written them
/// </summary>
[CsvRecordMap(typeof(CacheEntry))]
internal partial class CacheEntryMap
{
    [CsvColumn(Index = 0)] public int ControlSet { get; set; }
    [CsvColumn(Index = 1)] public int CacheEntryPosition { get; set; }
    [CsvColumn(Index = 2)] public string Path { get; set; }

    [CsvColumn(Index = 3)]
    [CsvConverter(typeof(DateTimeFormatConverter))]
    public DateTimeOffset? LastModifiedTimeUTC { get; set; }

    [CsvColumn(Index = 4)] public AppCompatCache.AppCompatCache.Execute Executed { get; set; }
    [CsvColumn(Index = 5)] public bool Duplicate { get; set; }
    [CsvColumn(Index = 6)] public string SourceFile { get; set; }
    [CsvColumn(Index = 7)] public bool FromSlack { get; set; }

    [CsvIgnore] public int CacheEntrySize { get; set; }
    [CsvIgnore] public byte[] Data { get; set; }
    [CsvIgnore] public AppCompatCache.AppCompatCache.InsertFlag InsertFlags { get; set; }
    [CsvIgnore] public int DataSize { get; set; }
    [CsvIgnore] public DateTimeOffset? LastUpdateTimeUTC { get; set; }
    [CsvIgnore] public int PathSize { get; set; }
    [CsvIgnore] public string Signature { get; set; }
}

/// <summary>Writes a time stamp in the --dt format (<see cref="Format" />, set once at startup), in the current culture as CsvHelper 33 did</summary>
internal sealed class DateTimeFormatConverter : ICsvConverter<DateTimeOffset>
{
    public static string Format { get; set; } = "yyyy-MM-dd HH:mm:ss";

    public DateTimeOffset Read(CsvReader reader, int index) => DateTimeOffset.ParseExact(reader.GetString(index), Format, CultureInfo.CurrentCulture);

    public void Write(CsvWriter writer, DateTimeOffset value) => writer.WriteField(value.ToString(Format));
}

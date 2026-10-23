# AppCompatCacheParser

Parses the AppCompatCache (ShimCache) from a SYSTEM hive, or from the live Registry, to CSV: each entry's path, last
modified time and whether it was executed (where the format has it), for every control set, plus entries recovered from
the slack of the value. Supports Windows XP, Windows Server 2003, Vista and 2008, Windows 7 (x86 and x64), Windows 8.x,
Windows 10 and Windows 11.

| | |
|---|---|
| **Version** | 2026.10.0 |
| **Download** | [.NET 10](https://download.ericzimmermanstools.com/net/AppCompatCacheParser.zip) (recommended) &middot; [.NET Framework 4.7.2](https://download.ericzimmermanstools.com/AppCompatCacheParser.zip) |
| **All tools** | [ericzimmerman.github.io](https://ericzimmerman.github.io) or [Get-ZimmermanTools](https://download.ericzimmermanstools.com/Get-ZimmermanTools.zip), which downloads them all and keeps them current |
| **Changes** | [ChangeLog.txt](https://download.ericzimmermanstools.com/ChangeLog.txt) |
| **Issues** | [EricZimmerman/AppCompatCacheParser](https://github.com/EricZimmerman/AppCompatCacheParser/issues) or [EricZimmerman/Issues](https://github.com/EricZimmerman/Issues/issues) |

## Requirements

- **.NET 10 build**: the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Run
  `AppCompatCacheParser.exe` on Windows, or `dotnet AppCompatCacheParser.dll` on any system with the runtime (with `-f`:
  the live Registry is Windows only).
- **.NET Framework build**: .NET Framework 4.7.2 or later (Windows only).

## Command line

```text
Usage:
  AppCompatCacheParser [options]

Options:
  -f <f>          Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used
  --csv <csv>     Directory to save CSV formatted results to. Be sure to include the full path in double quotes
  --csvf <csvf>   File name to save CSV formatted results to. When present, overrides default name
  -c <c>          The ControlSet to parse. Default is to extract all control sets [default: -1]
  -t              Sorts last modified timestamps in descending order
  --dt <dt>       The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options [default: yyyy-MM-dd HH:mm:ss]
  --nl            When true, ignore transaction log files for dirty hives
  --debug         Show debug information during processing
  --trace         Show trace information during processing
  -?, -h, --help  Show help and usage information
  --version       Show version information
```

Short options (a single letter) start with one dash, long options with two.

## Examples

```text
AppCompatCacheParser.exe --csv c:\temp -t -c 2
AppCompatCacheParser.exe --csv c:\temp --csvf results.csv
```

## Output

`--csv` is required. The CSV is named `<timestamp>_<Windows version>_<hive name>_AppCompatCache.csv`
(`..._ControlSet00<n>_AppCompatCache.csv` with `-c`, and the computer's name instead of the hive's for the live Registry),
with these columns:

| Column | |
| --- | --- |
| ControlSet | The ControlSet00x the cache is in |
| CacheEntryPosition | The entry's place in the cache, most recently added or updated first (Windows XP: in the order of its LRU list) |
| Path | The file's path. Packaged (UWP) app entries on Windows 10 and 11 have tab separated fields here instead, and no time |
| LastModifiedTimeUTC | The file's last modified time ($STANDARD_INFORMATION) when it was added to the cache |
| Executed | Windows 10/11: the entry's data has tag 0x100. Windows 7, 8.x and 64 bit Vista/2008: insertion flag 0x2. NA where the format has neither |
| Duplicate | The same path and time is already in an earlier row (another control set, for example) |
| SourceFile | The hive |
| FromSlack | Recovered from the slack of the value's cell: no longer in the cache (Windows 8 to 11; only entries that are not in it) |

A dirty hive gets its transaction logs (`SYSTEM.LOG`, `.LOG1`, `.LOG2` next to it, in any case) replayed; without them it
is an error unless `--nl` is given. A hive that is in use is read raw (Windows, administrator), with its logs. A damaged
entry is reported and the entries that could be read are still written. No CSV is written when there is no cache.

## Library

The `AppCompatCache` NuGet package ([NuGet](https://www.nuget.org/packages/AppCompatCache)) parses the value from a hive,
or the raw value:

```csharp
var acc = new AppCompatCache.AppCompatCache(@"C:\Temp\SYSTEM", -1, false); //all control sets, replaying transaction logs
foreach (var cache in acc.Caches)
{
    foreach (var e in cache.Entries.Concat(cache.SlackEntries))
    {
        Console.WriteLine($"{e.ControlSet} {e.CacheEntryPosition} {e.Path} {e.LastModifiedTimeUTC} {e.Executed}");
    }

    foreach (var error in cache.ParseErrors) //entries that could not be read
    {
        Console.WriteLine(error);
    }
}

var fromValue = new AppCompatCache.AppCompatCache(File.ReadAllBytes("AppCompatCache.bin"), 1, false);
```

- A value that is too small or not a known format throws `AppCompatCacheParseException`. A damaged entry is listed in
  `ParseErrors`, and the entries before it (or, for the fixed size formats of Windows XP to 7, the others) are still
  returned.
- Windows 10/11 entry data is a list of (tag, size, value) records: `Windows10.DataTags(entry.Data)`.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success. Damaged hives and cache entries are reported but are not a failure, nor is a hive without a cache |
| `1` | The tool failed: invalid arguments (no `--csv`) or a `-c` ControlSet that doesn't exist, a missing `-f` file, a hive (or the live Registry) it can't access or read, a dirty hive without its transaction logs (unless `--nl`), or a CSV that can't be written |

## What's new in 2026.10

- Hives load about 2x faster with less memory (Registry 2026.10.0); transaction logs are replayed as Windows does, and
  damaged hives that would not open are read.
- Windows 10/11 `Executed` is read from the entry's data records (tag 0x100) instead of "the last 4 bytes are 1", which
  missed 157 and wrongly flagged 102 of 9,316 entries in test hives (#6).
- New `FromSlack` column (last): entries recovered from the slack of the value's cell that are no longer in the cache
  (Windows 8 to 11).
- Windows XP: only the entries in use, most recently updated first (all 96 slots were written, including empty ones).
- `-t` sorts by last modified time (it did nothing); a control set without a cache no longer repeats the previous control
  set's entries, and control sets above 9 are found.
- A damaged entry is reported and the rest of the cache is still read; Windows 7 and Vista/2008 values are read with the
  right 32/64 bit layout when the hive's architecture value is missing or wrong.
- Exit code 1 when the tool fails (it was always 0). Error reports include source file and line numbers.

The full list is in the [ChangeLog](https://download.ericzimmermanstools.com/ChangeLog.txt).

## Building

```text
dotnet build AppCompatCacheParser.slnx -c Release
```

Builds `net10.0` and `net472`, the `AppCompatCache` library (`netstandard2.0`) and the tests. Hives are opened with the
[Registry](https://github.com/EricZimmerman/Registry) library ([NuGet](https://www.nuget.org/packages/Registry)).

## Further reading

- [Introducing AppCompatCacheParser](https://binaryforay.blogspot.com/2015/05/introducing-appcompatcacheparser.html)
- [AppCompatCacheParser v0.0.5.1 released](https://binaryforay.blogspot.com/2015/05/appcompatcacheparser-v0051-released.html)
- [AppCompatCacheParser v0.0.5.2 released](https://binaryforay.blogspot.com/2015/05/appcompatcacheparser-v0052-released.html)
- [AppCompatCacheParser v0.9.0.0 released and some AppCompatCache/shimcache parser testing](https://binaryforay.blogspot.com/2016/05/appcompatcacheparser-v0900-released-and.html)
- [Windows 10 Creators update vs shimcache parsers: Fight!!](https://binaryforay.blogspot.com/2017/03/windows-10-creators-update-vs-shimcache.html)
- [Updates to the left of me, updates to the right of me, version 1 releases are here (for the most part)](https://binaryforay.blogspot.com/2018/03/updates-to-left-of-me-updates-to-right.html)
- [Everything gets an update, Sept 2018 edition](https://binaryforay.blogspot.com/2018/09/everything-gets-update-sept-2018-edition.html)
- [Locked file support added to AmcacheParser, AppCompatCacheParser, MFTECmd, ShellBags Explorer (and SBECmd), and Registry Explorer (and RECmd)](https://binaryforay.blogspot.com/2019/01/locked-file-support-added-to.html)
- [Windows Registry Knowledge Base: Application compatibility cache](https://github.com/libyal/winreg-kb/blob/main/docs/sources/system-keys/Application-compatibility-cache.md)

## Special thanks

Open source development funding and support provided by the [SANS Institute](https://www.sans.org/) and
[SANS DFIR](https://www.sans.org/digital-forensics-incident-response/).

## License

[MIT](LICENSE.md), Copyright (c) Eric R. Zimmerman 2027.

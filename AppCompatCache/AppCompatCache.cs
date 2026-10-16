using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Alphaleonis.Win32.Security;
using RawCopy;
using Registry;
using Registry.Abstractions;
using Serilog;

namespace AppCompatCache;

public class AppCompatCache
{
    public enum Execute
    {
        Yes,
        No,
        NA
    }

    [Flags]
    public enum InsertFlag
    {
        Unknown1 = 0x00000001,
        Executed = 0x00000002,
        Unknown4 = 0x00000004,
        Unknown8 = 0x00000008,
        Unknown10 = 0x00000010,
        Unknown20 = 0x00000020,
        Unknown40 = 0x00000040,
        Unknown80 = 0x00000080,
        Unknown10000 = 0x00010000,
        Unknown20000 = 0x00020000,
        Unknown30000 = 0x00030000,
        Unknown40000 = 0x00040000,
        Unknown100000 = 0x00100000,
        Unknown200000 = 0x00200000,
        Unknown400000 = 0x00400000,
        Unknown800000 = 0x00800000
    }

    public enum OperatingSystemVersion
    {
        WindowsXP,
        WindowsVistaWin2k3Win2k8,
        Windows7x86,
        Windows7x64_Windows2008R2,
        Windows80_Windows2012,
        Windows81_Windows2012R2,
        Windows10,
        Windows10C_11,
        Unknown
    }

    private const string CacheKey = @"Control\Session Manager\AppCompatCache";

    //Windows XP
    private const string CacheKeyXp = @"Control\Session Manager\AppCompatibility";

    public AppCompatCache(byte[] rawBytes, int controlSet, bool is32Bit)
    {
        Caches = new List<IAppCompatCache>();
        ControlSet = controlSet;
        var cache = Init(rawBytes, is32Bit, controlSet);
        Caches.Add(cache);
    }

    /// <param name="filename">A SYSTEM hive, or null or empty for the live Registry (Windows only)</param>
    /// <param name="controlSet">The ControlSet00x to read, or -1 for all of them</param>
    /// <param name="noLogs">Don't replay the transaction logs of a dirty hive</param>
    public AppCompatCache(string filename, int controlSet, bool noLogs)
    {
        Caches = new List<IAppCompatCache>();

        if (string.IsNullOrEmpty(filename))
        {
            LoadLiveRegistry();
            return;
        }

        ControlSet = controlSet;

        PrivilegeEnabler enabler = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //for raw reads of hives and logs that are in use
            enabler = new PrivilegeEnabler(Privilege.Backup, Privilege.EnableDelegation, Privilege.Impersonate, Privilege.Tcb);
        }

        try
        {
            var reg = LoadHive(filename, noLogs);
            LoadCaches(reg, filename, controlSet);
        }
        finally
        {
            enabler?.Dispose();
        }
    }

    public int ControlSet { get; private set; }

    public List<IAppCompatCache> Caches { get; }

    /// <summary>The format of the (last) cache</summary>
    public OperatingSystemVersion OperatingSystem { get; private set; }

    private void LoadLiveRegistry()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new NotSupportedException("Live Registry support is not supported on non-Windows platforms.");
        }

        var hklm = Microsoft.Win32.Registry.LocalMachine;
        var key = hklm.OpenSubKey($@"SYSTEM\CurrentControlSet\{CacheKey}") ?? hklm.OpenSubKey($@"SYSTEM\CurrentControlSet\{CacheKeyXp}");
        if (key == null)
        {
            Log.Error(@"'CurrentControlSet\{Key}' key not found!", CacheKey);
            return;
        }

        if (key.GetValue("AppCompatCache", null) is not byte[] rawBytes)
        {
            Log.Error(@"'AppCompatCache' value not found in 'CurrentControlSet\{Key}'!", CacheKey);
            return;
        }

        ControlSet = (int)hklm.OpenSubKey(@"SYSTEM\Select")!.GetValue("Current");

        Caches.Add(Init(rawBytes, Is32Bit(null, null), ControlSet));
    }

    /// <summary>The hive (read raw when it is in use), with its transaction logs replayed when it is dirty</summary>
    private static RegistryHive LoadHive(string filename, bool noLogs)
    {
        if (File.Exists(filename) == false && Helper.RawFileExists(filename) == false)
        {
            throw new FileNotFoundException($"File not found ({filename})!");
        }

        var dirname = Path.GetDirectoryName(Path.GetFullPath(filename));
        if (string.IsNullOrEmpty(dirname))
        {
            dirname = ".";
        }

        var hiveBase = Path.GetFileName(filename);

        RegistryHive reg;
        List<TransactionLogFileInfo> rawLogs = null;

        try
        {
            reg = new RegistryHive(filename) { RecoverDeleted = false };
        }
        catch (IOException)
        {
            //in use: read it, and its logs, raw
            if (Helper.IsAdministrator() == false)
            {
                throw new UnauthorizedAccessException("Administrator privileges not found!");
            }

            Log.Warning("'{Filename}' is in use. Rerouting...", filename);

            var files = new List<string> { filename };
            files.AddRange(FindLogs(dirname, hiveBase, true));

            var rawFiles = Helper.GetRawFiles(files);
            reg = new RegistryHive(ReadAll(rawFiles.First().FileStream), rawFiles.First().InputFilename) { RecoverDeleted = false };
            rawLogs = rawFiles.Skip(1).Select(t => new TransactionLogFileInfo(t.InputFilename, ReadAll(t.FileStream))).ToList();
        }

        if (reg.Header.PrimarySequenceNumber == reg.Header.SecondarySequenceNumber)
        {
            reg.ParseHive();
            return reg;
        }

        if (noLogs)
        {
            Log.Warning("Registry hive is dirty, but --nl was provided, so its transaction logs are not replayed. Data may be missing! Continuing anyways...");
            reg.ParseHive();
            return reg;
        }

        var logFiles = rawLogs == null ? FindLogs(dirname, hiveBase, false) : null;
        if (rawLogs is { Count: > 0 })
        {
            reg.ProcessTransactionLogs(rawLogs, true);
        }
        else if (logFiles is { Count: > 0 })
        {
            reg.ProcessTransactionLogs(logFiles, true);
        }
        else
        {
            Log.Warning("Registry hive is dirty and no transaction logs were found in the same directory! LOGs should have same base name as the hive. Aborting!!");
            throw new Exception("Sequence numbers do not match and transaction logs were not found in the same directory as the hive. Aborting");
        }

        reg.ParseHive();
        return reg;
    }

    /// <summary>
    ///     The hive's transaction logs (SYSTEM.LOG, .LOG1, .LOG2, in any case) next to it; with <paramref name="raw" />,
    ///     also the ones that can only be read raw (in use)
    /// </summary>
    private static List<string> FindLogs(string dirname, string hiveBase, bool raw)
    {
        var names = new[] { ".LOG", ".LOG1", ".LOG2" }.Select(t => hiveBase + t).ToList();

        var logs = Directory.EnumerateFiles(dirname)
            .Where(f => names.Any(n => string.Equals(Path.GetFileName(f), n, StringComparison.OrdinalIgnoreCase)))
            .Where(f => new FileInfo(f).Length > 0)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (logs.Count == 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (raw || Helper.IsAdministrator())
            {
                logs.AddRange(names.Skip(1).Select(n => Path.Combine(dirname, n)).Where(Helper.RawFileExists));
            }
            else
            {
                Log.Fatal("Log files not found and no administrator access to look for them!");
            }
        }

        return logs;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private void LoadCaches(RegistryHive reg, string filename, int controlSet)
    {
        var controlSetIds = new List<int>();

        if (controlSet == -1)
        {
            foreach (var key in reg.Root?.SubKeys ?? new List<RegistryKey>())
            {
                var m = Regex.Match(key.KeyName, @"^ControlSet(\d{3})$", RegexOptions.IgnoreCase);
                if (m.Success && CacheKeyFor(reg, int.Parse(m.Groups[1].Value)) != null)
                {
                    controlSetIds.Add(int.Parse(m.Groups[1].Value));
                }
            }

            controlSetIds.Sort();

            if (controlSetIds.Count > 1)
            {
                Log.Warning("***The following ControlSet00x keys will be exported: {Cs}. Use -c to process keys individually", string.Join(",", controlSetIds));
            }
        }
        else
        {
            if (CacheKeyFor(reg, controlSet) == null)
            {
                throw new Exception($"Could not find ControlSet00{controlSet}");
            }

            controlSetIds.Add(controlSet);
        }

        var is32 = Is32Bit(filename, reg);

        Log.Debug("**** Found {Count} ids to process", controlSetIds.Count);

        foreach (var id in controlSetIds)
        {
            Log.Debug("**** Processing id {Id}", id);

            var value = CacheKeyFor(reg, id)?.Values.FirstOrDefault(c => c.ValueName == "AppCompatCache");
            if (value?.ValueDataRaw == null)
            {
                Log.Error("'AppCompatCache' value not found for 'ControlSet{Id:000}'! Skipping", id);
                continue;
            }

            var cache = Init(value.ValueDataRaw, is32, id);
            //only what isn't still in the cache (the slack usually holds an older copy of entries that still are)
            var live = new HashSet<string>(cache.Entries.Select(t => t.GetKey()));
            var recovered = Slack.Recover(value.ValueSlackRaw, OperatingSystem, id, 0).Where(t => live.Contains(t.GetKey()) == false).ToList();
            for (var i = 0; i < recovered.Count; i++)
            {
                recovered[i].CacheEntryPosition = cache.Entries.Count + i;
            }

            cache.SlackEntries.AddRange(recovered);
            Caches.Add(cache);
        }
    }

    private static RegistryKey CacheKeyFor(RegistryHive reg, int controlSet)
    {
        return reg.GetKey($@"ControlSet{controlSet:000}\{CacheKey}") ?? reg.GetKey($@"ControlSet{controlSet:000}\{CacheKeyXp}");
    }

    //https://github.com/libyal/winreg-kb/blob/main/docs/sources/system-keys/Application-compatibility-cache.md
    //https://dl.mandiant.com/EE/library/Whitepaper_ShimCacheParser.pdf

    private IAppCompatCache Init(byte[] rawBytes, bool is32, int controlSet)
    {
        OperatingSystem = OperatingSystemVersion.Unknown;

        if (rawBytes == null || rawBytes.Length < 8)
        {
            throw new AppCompatCacheParseException($"The AppCompatCache value is too small ({rawBytes?.Length ?? 0} bytes)", 0, "Header");
        }

        var sigNum = BitConverter.ToUInt32(rawBytes, 0);
        var signature = Bytes.Ascii(rawBytes, 128, 4);

        Log.Debug("**** Signature {Signature}, Sig num {SigNum}", signature, $"0x{sigNum:X}");

        IAppCompatCache appCache = null;

        if (sigNum == 0xDEADBEEF)
        {
            OperatingSystem = OperatingSystemVersion.WindowsXP;
            appCache = new WindowsXP(rawBytes, is32, controlSet);
        }
        else if (sigNum == 0xBADC0FFE)
        {
            OperatingSystem = OperatingSystemVersion.WindowsVistaWin2k3Win2k8;
            appCache = new VistaWin2k3Win2k8(rawBytes, is32, controlSet);
        }
        else if (sigNum == 0xBADC0FEE)
        {
            var win7 = new Windows7(rawBytes, is32, controlSet);
            OperatingSystem = win7.Is32Bit ? OperatingSystemVersion.Windows7x86 : OperatingSystemVersion.Windows7x64_Windows2008R2;
            appCache = win7;
        }
        else if (signature == "00ts")
        {
            OperatingSystem = OperatingSystemVersion.Windows80_Windows2012;
            appCache = new Windows8x(rawBytes, OperatingSystem, controlSet);
        }
        else if (signature == "10ts")
        {
            OperatingSystem = OperatingSystemVersion.Windows81_Windows2012R2;
            appCache = new Windows8x(rawBytes, OperatingSystem, controlSet);
        }
        else if (sigNum >= 0x30 && sigNum < rawBytes.Length && Bytes.Ascii(rawBytes, sigNum, 4) == "10ts")
        {
            //Windows 10 and 11: the header size, then the entries
            OperatingSystem = sigNum == 0x34 ? OperatingSystemVersion.Windows10C_11 : OperatingSystemVersion.Windows10;
            appCache = new Windows10(rawBytes, controlSet);
        }

        if (appCache == null)
        {
            var start = BitConverter.ToString(rawBytes, 0, Math.Min(16, rawBytes.Length));
            throw new AppCompatCacheParseException(
                $"Unable to determine operating system! The AppCompatCache value ({rawBytes.Length:N0} bytes) starts with {start}. If it is a valid cache, please send the hive to saericzimmerman@gmail.com",
                0, "Signature");
        }

        return appCache;
    }

    /// <summary>
    ///     Whether the system is 32 bit (PROCESSOR_ARCHITECTURE in the current control set, or the live Registry's when
    ///     <paramref name="reg" /> is null). When the hive doesn't say, 64 bit; the Windows 7 and Vista layouts are also
    ///     checked against the data
    /// </summary>
    public static bool Is32Bit(string fileName, RegistryHive reg)
    {
        if (reg == null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("'Filename' is required on non-Windows platforms.");
            }

            var subKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
            var val = subKey?.GetValue("PROCESSOR_ARCHITECTURE");
            if (val != null)
            {
                return val.ToString().Equals("x86");
            }

            throw new NullReferenceException("Unable to determine CPU architecture!");
        }

        var current = reg.GetKey("Select")?.Values.FirstOrDefault(c => c.ValueName == "Current")?.ValueData;
        if (int.TryParse(current, out var currentCtlSet))
        {
            var arch = reg.GetKey($@"ControlSet{currentCtlSet:000}\Control\Session Manager\Environment")
                ?.Values.FirstOrDefault(c => c.ValueName == "PROCESSOR_ARCHITECTURE")?.ValueData;
            if (arch != null)
            {
                return arch.Equals("x86");
            }
        }

        Log.Warning("Unable to determine the CPU architecture of '{File}' (no Select\\Current or PROCESSOR_ARCHITECTURE). Assuming 64 bit", fileName);
        return false;
    }
}

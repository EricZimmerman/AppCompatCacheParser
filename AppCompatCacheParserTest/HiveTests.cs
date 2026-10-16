using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Acc = AppCompatCache.AppCompatCache;

namespace AppCompatCacheTest;

/// <summary>
///     The DFIR Artifact Museum's Windows 11 SYSTEM hive (dirty, with its transaction logs), through the library and
///     AppCompatCacheParser (the built AppCompatCacheParser.dll, run as a separate process)
/// </summary>
[TestFixture]
public class HiveTests
{
    private string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "accp-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_dir, true);
    }

    /// <summary>The hive (and, with <paramref name="logs" />, its logs) decompressed into the test's folder</summary>
    private string Hive(bool logs = true)
    {
        var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", "DFIRArtifactMuseum", "Win11", "RathbunVM");
        foreach (var name in logs ? new[] { "SYSTEM", "SYSTEM.LOG1", "SYSTEM.LOG2" } : new[] { "SYSTEM" })
        {
            using var gz = new GZipStream(File.OpenRead(Path.Combine(source, name + ".gz")), CompressionMode.Decompress);
            using var output = File.Create(Path.Combine(_dir, name));
            gz.CopyTo(output);
        }

        return Path.Combine(_dir, "SYSTEM");
    }

    [Test]
    public void DirtyHiveWithLogs()
    {
        var acc = new Acc(Hive(), -1, false);

        acc.OperatingSystem.Should().Be(Acc.OperatingSystemVersion.Windows10C_11);
        var cache = acc.Caches.Single();
        cache.ControlSet.Should().Be(1);
        cache.ParseErrors.Should().BeEmpty();
        cache.Entries.Should().HaveCount(359);
        cache.Entries.Count(t => t.Executed == Acc.Execute.Yes).Should().Be(30);
        //packaged apps: a tab separated "path" and no time
        cache.Entries.Count(t => t.Path.Contains('\t')).Should().Be(151);
        cache.Entries.Where(t => t.Path.Contains('\t')).Should().OnlyContain(t => t.LastModifiedTimeUTC == null);
        //as regipy (ShimCacheParser) gives them after replaying the same logs
        cache.Entries[0].Path.Should().Be(@"C:\Windows\SoftwareDistribution\Download\Install\AM_Delta_Patch_1.357.209.0.exe");
        cache.Entries[0].LastModifiedTimeUTC.Should().Be(new DateTimeOffset(2022, 2, 7, 7, 37, 13, TimeSpan.Zero).AddTicks(4341508));
        cache.Entries[1].Path.Should().Be(@"C:\Windows\System32\PickerHost.exe");
        //older copies of entries that are still in the cache, so none are no longer in it
        cache.SlackEntries.Should().BeEmpty();
    }

    [Test]
    public void DirtyHiveWithoutLogs()
    {
        var hive = Hive(false);

        FluentActions.Invoking(() => new Acc(hive, -1, false)).Should().Throw<Exception>().WithMessage("Sequence numbers do not match*");
        new Acc(hive, -1, true).Caches.Single().Entries.Should().HaveCount(359);
    }

    [Test]
    public void ControlSetThatIsNotThere()
    {
        FluentActions.Invoking(() => new Acc(Hive(), 5, false)).Should().Throw<Exception>().WithMessage("Could not find ControlSet005");
    }

    private (int ExitCode, string Output) Run(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _dir
        };
        start.ArgumentList.Add(Path.Combine(TestContext.CurrentContext.TestDirectory, "AppCompatCacheParser.dll"));
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardInput.Close();

        process.WaitForExit(300_000).Should().BeTrue("AppCompatCacheParser should finish");
        return (process.ExitCode, stdout.Result + stderr.Result);
    }

    [Test]
    public void Csv()
    {
        var hive = Hive();
        var (code, output) = Run("-f", hive, "--csv", Path.Combine(_dir, "out"), "--csvf", "a.csv");

        code.Should().Be(0, output);
        var lines = File.ReadAllLines(Path.Combine(_dir, "out", "a.csv"));
        lines[0].Should().Be("ControlSet,CacheEntryPosition,Path,LastModifiedTimeUTC,Executed,Duplicate,SourceFile,FromSlack");
        lines.Should().HaveCount(360);
        lines[1].Should().Be($@"1,0,C:\Windows\SoftwareDistribution\Download\Install\AM_Delta_Patch_1.357.209.0.exe,2022-02-07 07:37:13,No,False,{hive},False");
        output.Should().Contain("Found 359 cache entries for Windows10C_11 in ControlSet001");
    }

    [Test]
    public void SortedByTime()
    {
        var (code, output) = Run("-f", Hive(), "--csv", Path.Combine(_dir, "out"), "--csvf", "a.csv", "-t", "--dt", "yyyy-MM-dd HH:mm:ss.fffffff");

        code.Should().Be(0, output);
        //-t did nothing
        var times = File.ReadAllLines(Path.Combine(_dir, "out", "a.csv")).Skip(1).Select(t => t.Split(',')[3]).Where(t => t.Length > 0).ToList();
        times.Should().HaveCount(208).And.BeInDescendingOrder(StringComparer.Ordinal);
    }

    [Test]
    public void ExitCodes()
    {
        Run("-f", Path.Combine(_dir, "missing"), "--csv", _dir).ExitCode.Should().Be(1);
        Run("-f", Hive(false), "--csv", Path.Combine(_dir, "out")).ExitCode.Should().Be(1, "the hive is dirty and has no logs");
        Run("-f", Path.Combine(_dir, "SYSTEM"), "--csv", Path.Combine(_dir, "out"), "--nl").ExitCode.Should().Be(0);
        Run("-f", Path.Combine(_dir, "SYSTEM"), "--csv", Path.Combine(_dir, "out"), "--nl", "-c", "7").ExitCode.Should().Be(1);
        Run("-f", Path.Combine(_dir, "SYSTEM")).ExitCode.Should().Be(1, "--csv is required");

        //no CSV is written when there is nothing to put in it
        var notAHive = Path.Combine(_dir, "junk");
        File.WriteAllBytes(notAHive, new byte[4096]);
        Run("-f", notAHive, "--csv", Path.Combine(_dir, "junkout")).ExitCode.Should().Be(1);
        Directory.Exists(Path.Combine(_dir, "junkout")).Should().BeFalse();
    }
}

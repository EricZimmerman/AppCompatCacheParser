using System.IO;
using System.Linq;
using AppCompatCache;
using FluentAssertions;
using NUnit.Framework;
using Acc = AppCompatCache.AppCompatCache;

namespace AppCompatCacheTest;

/// <summary>
///     The AppCompatCache values in TestFiles (also in the DFIR Artifact Museum). Counts agree with regipy's
///     ShimCacheParser (Mandiant's) where it can read them, apart from packaged app entries it skips
/// </summary>
[TestFixture]
public class SampleTests
{
    private static byte[] Sample(string name) => File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", name));

    [TestCase("WinXPx86.bin", true, Acc.OperatingSystemVersion.WindowsXP, 17)]
    [TestCase("Win2k8Standard.bin", false, Acc.OperatingSystemVersion.WindowsVistaWin2k3Win2k8, 873)]
    [TestCase("win7x86.bin", true, Acc.OperatingSystemVersion.Windows7x86, 91)]
    [TestCase("win7x64.bin", false, Acc.OperatingSystemVersion.Windows7x64_Windows2008R2, 304)]
    [TestCase("Win80.bin", false, Acc.OperatingSystemVersion.Windows80_Windows2012, 104)]
    [TestCase("Win81.bin", false, Acc.OperatingSystemVersion.Windows81_Windows2012R2, 1024)]
    [TestCase("Win10.bin", false, Acc.OperatingSystemVersion.Windows10, 350)]
    [TestCase("Win10Creators.bin", false, Acc.OperatingSystemVersion.Windows10C_11, 506)]
    public void Counts(string name, bool is32Bit, Acc.OperatingSystemVersion os, int count)
    {
        var acc = new Acc(Sample(name), 1, is32Bit);

        acc.OperatingSystem.Should().Be(os);
        var cache = acc.Caches.Single();
        cache.Entries.Should().HaveCount(count);
        cache.ParseErrors.Should().BeEmpty();
        cache.Entries.Select(t => t.CacheEntryPosition).Should().Equal(Enumerable.Range(0, count));
        cache.Entries.Should().OnlyContain(t => t.ControlSet == 1 && t.Path != null && t.Path.StartsWith(@"\??\") == false);
    }

    [Test]
    public void Windows2008()
    {
        var a = new VistaWin2k3Win2k8(Sample("Win2k8Standard.bin"), false, -1);

        a.EntryCount.Should().Be(873);
        Check(a.Entries[0], 164, Acc.Execute.Yes, "raw_agent_svc.exe");
        Check(a.Entries[2], 62, Acc.Execute.Yes, "mmc.exe");
        Check(a.Entries[5], 62, Acc.Execute.No, "SCW.exe");
        Check(a.Entries[337], 148, Acc.Execute.Yes, "MSExchangeTransport.exe");
        Check(a.Entries[349], 136, Acc.Execute.Yes, "MSExchangeFDS.exe");
    }

    [Test]
    public void Windows7()
    {
        var x64 = new Windows7(Sample("win7x64.bin"), false, -1);
        x64.EntryCount.Should().Be(304);
        Check(x64.Entries[0], 70, Acc.Execute.Yes, "wuauclt.exe");
        Check(x64.Entries[7], 126, Acc.Execute.No, "chrome.exe");
        Check(x64.Entries[301], 62, Acc.Execute.Yes, "reg.exe");

        var x86 = new Windows7(Sample("win7x86.bin"), true, -1);
        x86.EntryCount.Should().Be(91);
        Check(x86.Entries[0], 70, Acc.Execute.Yes, "LogonUI.exe");
        Check(x86.Entries[8], 108, Acc.Execute.No, "wmplayer.exe");
        Check(x86.Entries[90], 72, Acc.Execute.Yes, "SETUPUGC.EXE");
    }

    [Test]
    public void Windows8()
    {
        var w80 = new Windows8x(Sample("Win80.bin"), Acc.OperatingSystemVersion.Windows80_Windows2012, -1);
        w80.EntryCount.Should().Be(-1);
        Check(w80.Entries[0], 70, Acc.Execute.Yes, "LogonUI.exe");
        Check(w80.Entries[2], 144, Acc.Execute.Yes, "EditPadLite7.exe");
        Check(w80.Entries[101], 70, Acc.Execute.No, "WWAHost.exe");

        var w81 = new Windows8x(Sample("Win81.bin"), Acc.OperatingSystemVersion.Windows81_Windows2012R2, -1);
        Check(w81.Entries[0], 94, Acc.Execute.Yes, "java.exe");
        Check(w81.Entries[1011], 98, Acc.Execute.Yes, "osTriage2.exe");
        Check(w81.Entries[1023], 170, Acc.Execute.Yes, "setup.exe");
    }

    [Test]
    public void Windows10()
    {
        var w10 = new Windows10(Sample("Win10.bin"), -1);
        w10.EntryCount.Should().Be(-1);
        w10.ExpectedEntries.Should().Be(w10.Entries.Count);
        w10.Entries[0].PathSize.Should().Be(54);
        w10.Entries[0].Path.Should().Contain("vds.exe");
        w10.Entries[337].Path.Should().Contain("Ngen.exe");

        var creators = new Windows10(Sample("Win10Creators.bin"), -1);
        creators.ExpectedEntries.Should().Be(creators.Entries.Count);
        creators.Entries[0].Path.Should().Contain("nvstreg.exe");
        creators.Entries[349].Path.Should().Contain("SLUI.exe");

        //every entry's data is tag records, and executed means tag 0x100 is there
        foreach (var e in w10.Entries.Concat(creators.Entries))
        {
            var tags = AppCompatCache.Windows10.DataTags(e.Data).ToList();
            tags.Sum(t => 8 + ((t.Value.Length + 3) & ~3)).Should().Be(e.DataSize);
            e.Executed.Should().Be(tags.Any(t => t.Tag == 0x100) ? Acc.Execute.Yes : Acc.Execute.No);
        }
    }

    [Test]
    public void WindowsXp()
    {
        var a = new WindowsXP(Sample("WinXPx86.bin"), true, -1);

        //17 entries in the LRU array (of 96 slots), most recently updated first
        a.EntryCount.Should().Be(17);
        a.Entries.Should().HaveCount(17);
        a.Entries.Should().OnlyContain(t => t.Executed == Acc.Execute.NA && t.Path.Length > 0);
        a.Entries.Select(t => t.LastUpdateTimeUTC).Should().BeInDescendingOrder();
        a.Entries.Select(t => t.Path).Should().Contain(t => t.Contains("msoobe.exe")).And.Contain(t => t.Contains("NETSHELL.dll"));
    }

    private static void Check(CacheEntry e, int pathSize, Acc.Execute executed, string path)
    {
        e.PathSize.Should().Be(pathSize);
        e.Executed.Should().Be(executed);
        e.Path.Should().Contain(path);
    }
}

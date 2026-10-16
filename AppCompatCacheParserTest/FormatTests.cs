using System;
using System.Linq;
using AppCompatCache;
using FluentAssertions;
using NUnit.Framework;
using static AppCompatCacheTest.CacheBuilder;
using Acc = AppCompatCache.AppCompatCache;

namespace AppCompatCacheTest;

/// <summary>Values built in each format: what the parsers read from them, and damaged ones. Add regression tests here</summary>
[TestFixture]
public class FormatTests
{
    private static readonly byte[] One = U32(1);
    private static readonly byte[] Zero = U32(0);

    [Test]
    public void Windows10ExecutedIsTag0x100()
    {
        //issue #6: "the last 4 bytes are 1" only works when tag 0x100 is the last record
        var value = Win10(true,
            Win10Entry(@"C:\a.exe", Ft(When), Tags((0x200, Zero), (0x800, new byte[] { 0x64, 0x86 }), (0x100, One), (0x20, Zero))),
            Win10Entry(@"C:\b.exe", Ft(When), Tags((0x200, Zero), (0x800, new byte[] { 0x64, 0x86 }), (0x40, One))),
            Win10Entry(@"C:\c.exe", Ft(When), Tags((0x200, One), (0x100, One))),
            Win10Entry(@"C:\d.exe", Ft(When), Tags((0x200, Zero), (0x800, new byte[] { 0x64, 0x86 }))));

        var acc = new Acc(value, 1, false);

        acc.OperatingSystem.Should().Be(Acc.OperatingSystemVersion.Windows10C_11);
        var entries = acc.Caches.Single().Entries;
        entries.Select(t => t.Path).Should().Equal(@"C:\a.exe", @"C:\b.exe", @"C:\c.exe", @"C:\d.exe");
        entries.Select(t => t.Executed).Should().Equal(Acc.Execute.Yes, Acc.Execute.No, Acc.Execute.Yes, Acc.Execute.No);
        entries[0].LastModifiedTimeUTC.Should().Be(new DateTimeOffset(When));
        Windows10.DataTags(entries[0].Data).Select(t => t.Tag).Should().Equal(0x200u, 0x800u, 0x100u, 0x20u);
    }

    [Test]
    public void Windows10EntriesWithLittleOrNoDataAreRead()
    {
        //an entry with less than 4 bytes of data stopped parsing (the executed check read before the data)
        var value = Win10(false,
            Win10Entry(@"C:\a.exe", Ft(When), Array.Empty<byte>()),
            Win10Entry(@"C:\b.exe", 0, new byte[] { 1, 2 }),
            Win10Entry(@"C:\c.exe", Ft(When), Tags((0x100, One))));

        var acc = new Acc(value, 1, false);

        acc.OperatingSystem.Should().Be(Acc.OperatingSystemVersion.Windows10);
        var cache = (Windows10)acc.Caches.Single();
        cache.Entries.Select(t => t.Path).Should().Equal(@"C:\a.exe", @"C:\b.exe", @"C:\c.exe");
        cache.Entries[1].LastModifiedTimeUTC.Should().BeNull();
        cache.Entries[2].Executed.Should().Be(Acc.Execute.Yes);
        cache.ExpectedEntries.Should().Be(3);
        cache.ParseErrors.Should().BeEmpty();
    }

    [Test]
    public void TruncatedWindows10ValueKeepsTheEntriesBefore()
    {
        var value = Win10(true, Win10Entry(@"C:\a.exe", Ft(When), Tags((0x100, One))), Win10Entry(@"C:\b.exe", Ft(When), Tags((0x100, One))));
        var cut = value.Take(value.Length - 6).ToArray();

        var cache = new Acc(cut, 1, false).Caches.Single();

        cache.Entries.Select(t => t.Path).Should().Equal(@"C:\a.exe");
        cache.ParseErrors.Should().ContainSingle().Which.Should().Contain("Entry 1");
    }

    [Test]
    public void Windows8()
    {
        var value = Win8(true, (@"\??\C:\a.exe", Ft(When), 0x2, new byte[] { 9, 9, 9 }), (@"C:\b.exe", Ft(When), 0x4, Array.Empty<byte>()));

        var acc = new Acc(value, 1, false);

        acc.OperatingSystem.Should().Be(Acc.OperatingSystemVersion.Windows81_Windows2012R2);
        var entries = acc.Caches.Single().Entries;
        entries.Select(t => t.Path).Should().Equal(@"C:\a.exe", @"C:\b.exe");
        entries.Select(t => t.Executed).Should().Equal(Acc.Execute.Yes, Acc.Execute.No);
        entries[1].Data.Should().Equal(U32(0xAABBCCDD));

        new Acc(Win8(false, (@"C:\a.exe", Ft(When), 0, Array.Empty<byte>())), 1, false).OperatingSystem.Should().Be(Acc.OperatingSystemVersion.Windows80_Windows2012);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Windows7LayoutIsCheckedAgainstTheData(bool is32Bit)
    {
        var value = Table(true, is32Bit, (@"\??\C:\a.exe", Ft(When), 0x2), (@"C:\b.exe", Ft(When), 0));

        //the architecture value can be wrong (or missing, when 64 bit is assumed)
        foreach (var hint in new[] { true, false })
        {
            var acc = new Acc(value, 1, hint);

            acc.OperatingSystem.Should().Be(is32Bit ? Acc.OperatingSystemVersion.Windows7x86 : Acc.OperatingSystemVersion.Windows7x64_Windows2008R2);
            var cache = acc.Caches.Single();
            cache.EntryCount.Should().Be(2);
            cache.Entries.Select(t => t.Path).Should().Equal(@"C:\a.exe", @"C:\b.exe");
            cache.Entries.Select(t => t.Executed).Should().Equal(Acc.Execute.Yes, Acc.Execute.No);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Vista(bool is32Bit)
    {
        var value = Table(false, is32Bit, (@"C:\a.exe", Ft(When), 0x2), (@"C:\b.exe", Ft(When), 0));

        var acc = new Acc(value, 1, !is32Bit);

        acc.OperatingSystem.Should().Be(Acc.OperatingSystemVersion.WindowsVistaWin2k3Win2k8);
        var cache = (VistaWin2k3Win2k8)acc.Caches.Single();
        cache.Is32Bit.Should().Be(is32Bit);
        cache.Entries.Select(t => t.Path).Should().Equal(@"C:\a.exe", @"C:\b.exe");
        //Windows 2003 has the file size where 32 bit Vista has the flags
        cache.Entries.Select(t => t.Executed).Should().Equal(is32Bit ? new[] { Acc.Execute.NA, Acc.Execute.NA } : new[] { Acc.Execute.Yes, Acc.Execute.No });
    }

    [Test]
    public void WindowsXpEntriesAreTheLruArrayInOrder()
    {
        //3 of 96 slots used; the LRU array lists them most recently updated first
        var value = Xp(new[] { 2, 0, 1 },
            (@"C:\old.exe", Ft(When.AddYears(-3)), Ft(When.AddDays(-2))),
            (@"C:\older.exe", Ft(When.AddYears(-4)), Ft(When.AddDays(-3))),
            (@"C:\new.exe", Ft(When.AddYears(-1)), Ft(When)));

        var acc = new Acc(value, 1, true);

        acc.OperatingSystem.Should().Be(Acc.OperatingSystemVersion.WindowsXP);
        var cache = acc.Caches.Single();
        cache.EntryCount.Should().Be(3);
        cache.Entries.Select(t => t.Path).Should().Equal(@"C:\new.exe", @"C:\old.exe", @"C:\older.exe");
        cache.Entries.Select(t => t.CacheEntryPosition).Should().Equal(0, 1, 2);
        cache.Entries[0].LastUpdateTimeUTC.Should().Be(new DateTimeOffset(When));
        cache.Entries[0].LastModifiedTimeUTC.Should().Be(new DateTimeOffset(When.AddYears(-1)));
        cache.Entries.Should().OnlyContain(t => t.Executed == Acc.Execute.NA);

        //64 bit hives too (XP x64 uses the Windows 2003 format, so this is always the 32 bit layout)
        new Acc(value, 1, false).Caches.Single().Entries.Should().HaveCount(3);
    }

    [Test]
    public void DamagedWindows7EntryIsReportedAndTheRestRead()
    {
        var value = Table(true, false, (@"C:\a.exe", Ft(When), 0x2), (@"C:\b.exe", Ft(When), 0), (@"C:\c.exe", Ft(When), 0));
        //entry 1's path offset past the end
        BitConverter.GetBytes(0x7FFFFFFFL).CopyTo(value, 128 + 48 + 8);

        var cache = new Acc(value, 1, false).Caches.Single();

        cache.Entries.Select(t => t.Path).Should().Equal(@"C:\a.exe", @"C:\c.exe");
        cache.ParseErrors.Should().ContainSingle().Which.Should().Contain("Entry 1");
    }

    [TestCase(new byte[0])]
    [TestCase(new byte[] { 1, 2, 3 })]
    public void TooSmallValuesThrow(byte[] value)
    {
        FluentActions.Invoking(() => new Acc(value, 1, false)).Should().Throw<AppCompatCacheParseException>().WithMessage("*too small*");
    }

    [Test]
    public void UnknownFormatsThrow()
    {
        //as in an image whose data ran out ("PAST END OF IMG!" filler)
        var junk = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(" END OF IMG!PAST", 64)));

        FluentActions.Invoking(() => new Acc(junk, 1, false)).Should().Throw<AppCompatCacheParseException>().WithMessage("Unable to determine operating system!*20-45-4E-44*");
    }

    [Test]
    public void RandomDamageDoesNotThrowUnexpectedExceptions()
    {
        var values = new[]
        {
            Win10(true, Win10Entry(@"C:\a.exe", Ft(When), Tags((0x100, One))), Win10Entry(@"C:\b.exe", Ft(When), Tags((0x40, One)))),
            Win8(true, (@"C:\a.exe", Ft(When), 0x2, new byte[] { 1 })),
            Table(true, false, (@"C:\a.exe", Ft(When), 0x2), (@"C:\b.exe", Ft(When), 0)),
            Table(false, true, (@"C:\a.exe", Ft(When), 0x2)),
            Xp(new[] { 1, 0 }, (@"C:\a.exe", Ft(When), Ft(When)), (@"C:\b.exe", Ft(When), Ft(When)))
        };

        var random = new Random(1234);
        foreach (var original in values)
        {
            for (var round = 0; round < 500; round++)
            {
                var b = (byte[])original.Clone();
                for (var i = 0; i < 8; i++)
                {
                    //keep the signature, so the format's parser is the one hit
                    var at = random.Next(4, b.Length);
                    b[at] = (byte)random.Next(256);
                }

                if (round % 5 == 0)
                {
                    b = b.Take(random.Next(8, b.Length)).ToArray();
                }

                try
                {
                    var acc = new Acc(b, 1, false);
                    acc.Caches.Single().Entries.Should().NotBeNull();
                }
                catch (AppCompatCacheParseException)
                {
                    //what unusable input gives
                }
            }
        }
    }

    [Test]
    public void SlackEntriesAreRecovered()
    {
        var entry = Win10Entry(@"C:\evicted.exe", Ft(When), Tags((0x100, One)));
        var other = Win10Entry(@"C:\also.exe", Ft(When.AddDays(-1)), Tags((0x200, Zero)));
        //the slack starts in the middle of an entry, has garbage, and ends with a cut off entry
        var slack = entry.Skip(20).Concat(new byte[] { 0x31, 0x30, 0x74, 0x73, 9, 9 }).Concat(entry).Concat(other).Concat(entry.Take(entry.Length - 3)).ToArray();

        var found = Slack.Recover(slack, Acc.OperatingSystemVersion.Windows10C_11, 1, 10);

        found.Select(t => t.Path).Should().Equal(@"C:\evicted.exe", @"C:\also.exe");
        found.Should().OnlyContain(t => t.FromSlack);
        found.Select(t => t.CacheEntryPosition).Should().Equal(10, 11);
        found[0].Executed.Should().Be(Acc.Execute.Yes);

        Slack.Recover(slack, Acc.OperatingSystemVersion.Windows7x64_Windows2008R2, 1, 0).Should().BeEmpty();
    }
}

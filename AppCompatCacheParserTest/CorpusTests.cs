using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Acc = AppCompatCache.AppCompatCache;

namespace AppCompatCacheTest;

/// <summary>
///     Every SYSTEM hive in a local collection that is not in the repository: Corpus/Registry next to the repository
///     (or Registry next to it), or ACC_CORPUS. Explicit (slow); skipped when the folder is missing
/// </summary>
[TestFixture]
[Explicit]
public class CorpusTests
{
    private static string CorpusPath()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("ACC_CORPUS");
        if (string.IsNullOrEmpty(fromEnvironment) == false)
        {
            return fromEnvironment;
        }

        for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
        {
            foreach (var candidate in new[] { Path.Combine(dir.FullName, "Corpus", "Registry"), Path.Combine(dir.FullName, "Registry") })
            {
                if (Directory.Exists(candidate) && dir.EnumerateDirectories("AppCompatCacheParser").Any())
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Hives whose embedded name is SYSTEM (not their transaction logs, which have the same header with another file type)</summary>
    private static IEnumerable<string> SystemHives(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var header = new byte[0x70];
            try
            {
                using var fs = File.OpenRead(file);
                if (fs.Read(header, 0, header.Length) < header.Length)
                {
                    continue;
                }
            }
            catch (IOException)
            {
                continue;
            }

            var name = Encoding.Unicode.GetString(header, 0x30, 0x40).Split('\0')[0];
            var primary = BitConverter.ToUInt32(header, 0x1C) == 0;
            if (Encoding.ASCII.GetString(header, 0, 4) == "regf" && primary && name.Split('\\').Last().Equals("SYSTEM", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    [Test]
    public void EverySystemHive()
    {
        var root = CorpusPath();
        if (root == null)
        {
            Assert.Ignore("No Corpus/Registry folder next to the repository (or ACC_CORPUS)");
        }

        var hives = SystemHives(root).ToList();
        var failures = new List<string>();
        var entries = 0;
        foreach (var hive in hives)
        {
            try
            {
                var acc = new Acc(hive, -1, true);
                entries += acc.Caches.Sum(t => t.Entries.Count);
                failures.AddRange(acc.Caches.SelectMany(t => t.ParseErrors).Select(t => $"{hive}: {t}"));
            }
            catch (AppCompatCache.AppCompatCacheParseException e)
            {
                failures.Add($"{hive}: {e.Message}");
            }
            catch (Exception e)
            {
                //the Registry library can't open some damaged hives; that is for it to fix
                TestContext.Out.WriteLine($"{hive}: {e.GetType().Name}: {e.Message}");
            }
        }

        TestContext.Out.WriteLine($"{hives.Count} hives, {entries:N0} entries");
        foreach (var failure in failures)
        {
            TestContext.Out.WriteLine(failure);
        }

        hives.Should().NotBeEmpty();
        //SYSTEM_BROKEN_Blanco, from an image that ended before the value's data: the value is " END OF IMG!PAST" filler
        failures.Should().OnlyContain(t => t.Contains("Unable to determine operating system") && t.Contains("20-45-4E-44-20-4F-46-20-49-4D-47"));
    }
}

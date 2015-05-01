using System.IO;
using AppCompatCache;
using NFluent;
using NUnit.Framework;

namespace AppCompatCacheTest
{
    [TestFixture]
    public class AppCompatCacheTest
    {
        [SetUp]
        public void PreTestSetup()
        {
            Win7X86 = File.ReadAllBytes(@"..\..\TestFiles\Win7x86.bin");
            Win7X64 = File.ReadAllBytes(@"..\..\TestFiles\Win7x64.bin");
            Win80 = File.ReadAllBytes(@"..\..\TestFiles\Win80.bin");
            Win81 = File.ReadAllBytes(@"..\..\TestFiles\Win81.bin");
            Win10 = File.ReadAllBytes(@"..\..\TestFiles\Win10.bin");
        }

        public byte[] Win7X86;
        public byte[] Win7X64;
        public byte[] Win80;
        public byte[] Win81;
        public byte[] Win10;

        [Test]
        public void Win10ShouldFindEntries()
        {
            var a = new Windows10(Win10);
            Check.That(a.Entries.Count).Equals(350);
        }

        [Test]
        public void Win7x64ShouldFindEntries()
        {
            var a = new Windows7(Win7X64, false);
            Check.That(a.Entries.Count).Equals(304);
        }

        [Test]
        public void Win7x86ShouldFindEntries()
        {
            var a = new Windows7(Win7X86, true);
            Check.That(a.Entries.Count).Equals(91);
        }

        [Test]
        public void Win80ShouldFindEntries()
        {
            var a = new Windows8x(Win80, AppCompatCache.AppCompatCache.OperatingSystemVersion.Windows80);
            Check.That(a.Entries.Count).Equals(58);
        }

        [Test]
        public void Win81ShouldFindEntries()
        {
            var a = new Windows8x(Win81, AppCompatCache.AppCompatCache.OperatingSystemVersion.Windows81Windows2012);
            Check.That(a.Entries.Count).Equals(1024);
        }
    }
}
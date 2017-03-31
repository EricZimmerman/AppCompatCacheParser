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
            Win10Creators = File.ReadAllBytes(@"..\..\TestFiles\Win10Creators.bin");
            WinXp = File.ReadAllBytes(@"..\..\TestFiles\WinXPx86.bin");
        }

        public byte[] Win7X86;
        public byte[] Win7X64;
        public byte[] Win80;
        public byte[] Win81;
        public byte[] Win10;
        public byte[] Win10Creators;
        public byte[] WinXp;

//        [Test]
//        public void OneOff()
//        {
//            var foo = File.ReadAllBytes(@"D:\Temp\Win2003SP2.bin");
//            var a = new VistaWin2k3Win2k8(foo, true, -1);
//        }


        [Test]
        public void Win10_CreatorsShouldFindEntries()
        {
            var a = new Windows10(Win10Creators, -1);
            Check.That(a.Entries.Count).Equals(506);
            Check.That(a.ExpectedEntries).Equals(a.Entries.Count);
            Check.That(a.EntryCount).Equals(-1);

            Check.That(a.Entries[0].PathSize).IsEqualTo(126);
            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[0].Path).Contains("nvstreg.exe");

            Check.That(a.Entries[2].PathSize).IsEqualTo(62);
            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[2].Path).Contains("grpconv.exe");

            Check.That(a.Entries[7].PathSize).IsEqualTo(166);
            Check.That(a.Entries[7].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[7].Path).Contains("ISBEW64.exe");

            Check.That(a.Entries[337].PathSize).IsEqualTo(64);
            Check.That(a.Entries[337].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[337].Path).Contains("wsqmcons.exe");

            Check.That(a.Entries[349].PathSize).IsEqualTo(56);
            Check.That(a.Entries[349].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[349].Path).Contains("SLUI.exe");
        }

        [Test]
        public void Win10ShouldFindEntries()
        {
            var a = new Windows10(Win10, -1);
            Check.That(a.Entries.Count).Equals(350);
            Check.That(a.ExpectedEntries).Equals(a.Entries.Count);
            Check.That(a.EntryCount).Equals(-1);

            Check.That(a.Entries[0].PathSize).IsEqualTo(54);
            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[0].Path).Contains("vds.exe");

            Check.That(a.Entries[2].PathSize).IsEqualTo(140);
            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[2].Path).Contains("DismHost.exe");

            Check.That(a.Entries[7].PathSize).IsEqualTo(58);
            Check.That(a.Entries[7].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[7].Path).Contains("mstsc.exe");

            Check.That(a.Entries[337].PathSize).IsEqualTo(112);
            Check.That(a.Entries[337].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[337].Path).Contains("Ngen.exe");

            Check.That(a.Entries[349].PathSize).IsEqualTo(64);
            Check.That(a.Entries[349].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[349].Path).Contains("services.exe");
        }

        [Test]
        public void Win7x64ShouldFindEntries()
        {
            var a = new Windows7(Win7X64, false, -1);
            Check.That(a.Entries.Count).Equals(304);
            Check.That(a.EntryCount).Equals(304);

            Check.That(a.Entries[0].PathSize).IsEqualTo(70);
            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[0].Path).Contains("wuauclt.exe");

            Check.That(a.Entries[2].PathSize).IsEqualTo(88);
            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[2].Path).Contains("SearchFilterHost.exe");

            Check.That(a.Entries[7].PathSize).IsEqualTo(126);
            Check.That(a.Entries[7].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.No);
            Check.That(a.Entries[7].Path).Contains("chrome.exe");

            Check.That(a.Entries[300].PathSize).IsEqualTo(176);
            Check.That(a.Entries[300].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[300].Path).Contains("chrmstp.exe");

            Check.That(a.Entries[301].PathSize).IsEqualTo(62);
            Check.That(a.Entries[301].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[301].Path).Contains("reg.exe");
        }


        [Test]
        public void WinXpx86ShouldFindEntries()
        {
            var a = new WindowsXP(WinXp, true, -1);
            Check.That(a.Entries.Count).Equals(17);
            Check.That(a.EntryCount).Equals(96);

            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[0].Path).Contains("msoobe.exe");

            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[2].Path).Contains("agentsvr.exe");

            Check.That(a.Entries[8].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.NA);
            Check.That(a.Entries[8].Path).Contains("NETSHELL.dll");

          
        }

        [Test]
        public void Win7x86ShouldFindEntries()
        {
            var a = new Windows7(Win7X86, true, -1);
            Check.That(a.Entries.Count).Equals(91);
            Check.That(a.EntryCount).Equals(91);

            Check.That(a.Entries[0].PathSize).IsEqualTo(70);
            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[0].Path).Contains("LogonUI.exe");

            Check.That(a.Entries[2].PathSize).IsEqualTo(92);
            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[2].Path).Contains("SearchProtocolHost.exe");

            Check.That(a.Entries[8].PathSize).IsEqualTo(108);
            Check.That(a.Entries[8].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.No);
            Check.That(a.Entries[8].Path).Contains("wmplayer.exe");

            Check.That(a.Entries[89].PathSize).IsEqualTo(62);
            Check.That(a.Entries[89].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[89].Path).Contains("reg.exe");

            Check.That(a.Entries[90].PathSize).IsEqualTo(72);
            Check.That(a.Entries[90].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[90].Path).Contains("SETUPUGC.EXE");
        }

        [Test]
        public void Win80ShouldFindEntries()
        {
            var a = new Windows8x(Win80, AppCompatCache.AppCompatCache.OperatingSystemVersion.Windows80_Windows2012, -1);
            Check.That(a.Entries.Count).Equals(104);
            Check.That(a.EntryCount).Equals(-1);


            Check.That(a.Entries[0].PathSize).IsEqualTo(70);
            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[0].Path).Contains("LogonUI.exe");

            Check.That(a.Entries[2].PathSize).IsEqualTo(144);
            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[2].Path).Contains("EditPadLite7.exe");

            Check.That(a.Entries[8].PathSize).IsEqualTo(70);
            Check.That(a.Entries[8].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[8].Path).Contains("svchost.exe");

            Check.That(a.Entries[100].PathSize).IsEqualTo(76);
            Check.That(a.Entries[100].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[100].Path).Contains("Setup.exe");

            Check.That(a.Entries[101].PathSize).IsEqualTo(70);
            Check.That(a.Entries[101].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.No);
            Check.That(a.Entries[101].Path).Contains("WWAHost.exe");
        }

        [Test]
        public void Win81ShouldFindEntries()
        {
            var a = new Windows8x(Win81, AppCompatCache.AppCompatCache.OperatingSystemVersion.Windows81_Windows2012R2,
                -1);
            Check.That(a.Entries.Count).Equals(1024);
            Check.That(a.EntryCount).Equals(-1);


            Check.That(a.Entries[0].PathSize).IsEqualTo(94);
            Check.That(a.Entries[0].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[0].Path).Contains("java.exe");

            Check.That(a.Entries[2].PathSize).IsEqualTo(128);
            Check.That(a.Entries[2].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[2].Path).Contains("SpotifyHelper.exe");

            Check.That(a.Entries[8].PathSize).IsEqualTo(70);
            Check.That(a.Entries[8].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[8].Path).Contains("dllhost.exe");

            Check.That(a.Entries[1011].PathSize).IsEqualTo(98);
            Check.That(a.Entries[1011].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[1011].Path).Contains("osTriage2.exe");

            Check.That(a.Entries[1023].PathSize).IsEqualTo(170);
            Check.That(a.Entries[1023].Executed).IsEqualTo(AppCompatCache.AppCompatCache.Execute.Yes);
            Check.That(a.Entries[1023].Path).Contains("setup.exe");
        }
    }
}
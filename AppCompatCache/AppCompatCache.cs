using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLog;
using Registry;
using Registry.Abstractions;

namespace AppCompatCache
{
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
            Unknown
        }

        public AppCompatCache(byte[] rawBytes, int controlSet)
        {
            Caches = new List<IAppCompatCache>();
            var cache = Init(rawBytes, false, controlSet);
            Caches.Add(cache);
        }

        public AppCompatCache(string filename, int controlSet)
        {
            byte[] rawBytes = null;
            Caches = new List<IAppCompatCache>();

            var controlSetIds = new List<int>();

            RegistryKey subKey = null;

            var isLiveRegistry = string.IsNullOrEmpty(filename);

            if (isLiveRegistry)
            {
                var keyCurrUser = Microsoft.Win32.Registry.LocalMachine;
                var subKey2 = keyCurrUser.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache");

                if (subKey2 == null)
                {
                    subKey2 =
                        keyCurrUser.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatibility");

                    if (subKey2 == null)
                    {
                        Console.WriteLine(
                            @"'CurrentControlSet\Control\Session Manager\AppCompatCache' key not found! Exiting");
                        return;
                    }
                }

                rawBytes = (byte[]) subKey2.GetValue("AppCompatCache", null);

                subKey2 = keyCurrUser.OpenSubKey(@"SYSTEM\Select");
                ControlSet = (int) subKey2.GetValue("Current");

                var is32Bit = Is32Bit(filename);

                var cache = Init(rawBytes, is32Bit, ControlSet);

                Caches.Add(cache);

                return;
            }


            ControlSet = controlSet;

            if (File.Exists(filename) == false)
            {
                throw new FileNotFoundException($"File not found ({filename})!");
            }

            var hive = new RegistryHiveOnDemand(filename);


            if (controlSet == -1)
            {
                for (var i = 0; i < 10; i++)
                {
                    subKey = hive.GetKey($@"ControlSet00{i}\Control\Session Manager\AppCompatCache");

                    if (subKey == null)
                    {
                        subKey = hive.GetKey($@"ControlSet00{i}\Control\Session Manager\AppCompatibility");
                    }

                    if (subKey != null)
                    {
                        controlSetIds.Add(i);
                    }
                }

                if (controlSetIds.Count > 1)
                {
                    var log = LogManager.GetCurrentClassLogger();

                    log.Warn(
                        $"***The following ControlSet00x keys will be exported: {string.Join(",", controlSetIds)}. Use -c to process keys individually\r\n");
                }
            }
            else
            {
                //a control set was passed in
                subKey = hive.GetKey($@"ControlSet00{ControlSet}\Control\Session Manager\AppCompatCache");

                if (subKey == null)
                {
                    subKey = hive.GetKey($@"ControlSet00{ControlSet}\Control\Session Manager\AppCompatibility");
                }

                if (subKey == null)
                {
                    throw new Exception($"Could not find ControlSet00{ControlSet}. Exiting");
                }

                controlSetIds.Add(ControlSet);
            }


            var is32 = Is32Bit(filename);

            var log1 = LogManager.GetCurrentClassLogger();

            log1.Debug($@"**** Found {controlSetIds.Count} ids to process");


            foreach (var id in controlSetIds)
            {
                log1.Debug($@"**** Processing id {id}");

                var hive2 = new RegistryHiveOnDemand(filename);

                subKey = hive2.GetKey($@"ControlSet00{id}\Control\Session Manager\AppCompatCache");



                if (subKey == null)
                {
                    log1.Debug($@"**** Initial subkey null, getting appCompatability key");
                    subKey = hive2.GetKey($@"ControlSet00{id}\Control\Session Manager\AppCompatibility");
                }

                log1.Debug($@"**** Looking  AppCompatcache value");

                var val = subKey?.Values.SingleOrDefault(c => c.ValueName == "AppCompatCache");

                if (val != null)
                {
                    log1.Debug($@"**** Found AppCompatcache value");
                    rawBytes = val.ValueDataRaw;
                }

                if (rawBytes == null)
                {
                    var log = LogManager.GetCurrentClassLogger();

                    log.Error($@"'AppCompatCache' value not found for 'ControlSet00{id}'! Exiting");
                }

                var cache = Init(rawBytes, is32, id);

                Caches.Add(cache);
            }
        }

        public int ControlSet { get; }

        public List<IAppCompatCache> Caches { get; }
        public OperatingSystemVersion OperatingSystem { get; private set; }

        //https://github.com/libyal/winreg-kb/wiki/Application-Compatibility-Cache-key
        //https://dl.mandiant.com/EE/library/Whitepaper_ShimCacheParser.pdf

        private IAppCompatCache Init(byte[] rawBytes, bool is32, int controlSet)
        {
            IAppCompatCache appCache = null;
            OperatingSystem = OperatingSystemVersion.Unknown;

            string signature;


            var sigNum = BitConverter.ToUInt32(rawBytes, 0);


            //TODO check minimum length of rawBytes and throw exception if not enough data

            signature = Encoding.ASCII.GetString(rawBytes, 128, 4);

            var log1 = LogManager.GetCurrentClassLogger();
            log1.Debug($@"**** Signature {signature}, Sig num 0x{sigNum:X}");

            if (sigNum == 0xDEADBEEF) //DEADBEEF, WinXp
            {
                OperatingSystem = OperatingSystemVersion.WindowsXP;
                
                log1.Debug(@"**** Processing XP hive");
                
                appCache = new WindowsXP(rawBytes, is32, controlSet);
            }
            else if (sigNum == 0xbadc0ffe)
            {
                OperatingSystem = OperatingSystemVersion.WindowsVistaWin2k3Win2k8;
                appCache = new VistaWin2k3Win2k8(rawBytes,is32,controlSet);   

            }
            else if (sigNum == 0xBADC0FEE) //BADC0FEE, Win7
            {
                if (is32)
                {
                    OperatingSystem = OperatingSystemVersion.Windows7x86;
                }
                else
                {
                    OperatingSystem = OperatingSystemVersion.Windows7x64_Windows2008R2;
                }

                appCache = new Windows7(rawBytes, is32, controlSet);
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
            else
            {
                //is it windows 10?
                signature = Encoding.ASCII.GetString(rawBytes, 48, 4);
                if (signature == "10ts")
                {
                    OperatingSystem = OperatingSystemVersion.Windows10;
                    appCache = new Windows10(rawBytes, controlSet);
                }
              
            }

            if (appCache == null)
            {
                throw new Exception("Unable to determine operating system! Please send the hive to saericzimmerman@gmail.com");
            }


            return appCache;
        }

        public static bool Is32Bit(string fileName)
        {
            if (fileName.Length == 0)
            {
                var keyCurrUser = Microsoft.Win32.Registry.LocalMachine;
                var subKey = keyCurrUser.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");

                var val = subKey?.GetValue("PROCESSOR_ARCHITECTURE");

                if (val != null)
                {
                    return val.ToString().Equals("x86");
                }
            }
            else
            {
                var hive = new RegistryHiveOnDemand(fileName);
                var subKey = hive.GetKey("Select");

                var currentCtlSet = int.Parse(subKey.Values.Single(c => c.ValueName == "Current").ValueData);

                subKey = hive.GetKey($"ControlSet00{currentCtlSet}\\Control\\Session Manager\\Environment");

                var val = subKey?.Values.SingleOrDefault(c => c.ValueName == "PROCESSOR_ARCHITECTURE");

                if (val != null)
                {
                    return val.ValueData.Equals("x86");
                }
            }

            throw new NullReferenceException("Unable to determine CPU architecture!");
        }
    }
}
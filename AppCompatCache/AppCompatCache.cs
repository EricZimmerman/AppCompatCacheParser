using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NLog;
using Registry;

namespace AppCompatCache
{
    public class AppCompatCache
    {
        public enum OperatingSystemVersion
        {
            WindowsXP,
            Windows7x86,
            Windows7x64_Windows2008R2,
            Windows80_Windows2012,
            Windows81_Windows2012R2,
            Windows10,
            Unknown
        }

        public enum Execute
        {
            Yes,
            No,
            Unknown
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
            Unknown800000 = 0x00800000,
        }

        public AppCompatCache(byte[] rawBytes, int controlSet)
        {
            Init(rawBytes, false,controlSet);
        }

        public AppCompatCache(string filename, int controlSet)
        {
            byte[] rawBytes = null;

            var isLiveRegistry = string.IsNullOrEmpty(filename);

            if (isLiveRegistry)
            {
                var keyCurrUser = Microsoft.Win32.Registry.LocalMachine;
                var subKey = keyCurrUser.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache");

                if (subKey == null)
                {
                    subKey = keyCurrUser.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatibility");

                    if (subKey == null)
                    {
                        Console.WriteLine(
                            @"'CurrentControlSet\Control\Session Manager\AppCompatCache' key not found! Exiting");
                        return;
                    }
                }

                rawBytes = (byte[]) subKey.GetValue("AppCompatCache", null);
            }
            else
            {

                ControlSet = controlSet;

                if (File.Exists(filename) == false)
                {
                    throw new FileNotFoundException($"File not found ({filename})!");
                }

                var hive = new RegistryHiveOnDemand(filename);
              
                var subKey = hive.GetKey("Select");

                if (controlSet == -1)
                {
                    if (subKey == null)
                    {
                        throw new Exception($"'Select' key not found. Is '{filename}' a system hive?");
                    }

                    ControlSet = int.Parse(subKey.Values.Single(c => c.ValueName == "Current").ValueData);
              
                   var sets = new List<int>();

                    for (int i = 0; i < 5; i++)
                    {
                        if (i == ControlSet)
                        {
                            continue;
                        }

                        subKey = hive.GetKey($@"ControlSet00{i}\Control\Session Manager\AppCompatCache");

                        if (subKey != null)
                        {
                            sets.Add(i);
                        }
                    }

                    if (sets.Any())
                    {
                        var _log = LogManager.GetCurrentClassLogger();

                        _log.Warn($"***Found the following additional ControlSet00x keys: {string.Join(",",sets)}. Use -c to process these keys\r\n");
                    }
                       
                }

                subKey = hive.GetKey($@"ControlSet00{ControlSet}\Control\Session Manager\AppCompatCache");

                if (subKey == null)
                {
                    subKey = hive.GetKey($@"ControlSet00{ControlSet}\Control\Session Manager\AppCompatibility");
                }

                if (subKey == null)
                {
                    throw new Exception($"Could not find ControlSet00{ControlSet}. Exiting");
                }

                var val = subKey?.Values.SingleOrDefault(c => c.ValueName == "AppCompatCache");

                if (val != null)
                {
                    rawBytes = val.ValueDataRaw;
                }
            }

            if (rawBytes == null)
            {
                Console.WriteLine(@"'AppCompatCache' value not found! Exiting");
                return;
            }

            var is32 = Is32Bit(filename);

            Init(rawBytes, is32,ControlSet);
        }

        public int ControlSet { get; }

        public IAppCompatCache Cache { get; private set; }
        public OperatingSystemVersion OperatingSystem { get; private set; }

        //https://github.com/libyal/winreg-kb/wiki/Application-Compatibility-Cache-key
        //https://dl.mandiant.com/EE/library/Whitepaper_ShimCacheParser.pdf

        private void Init(byte[] rawBytes, bool is32, int controlSet)
        {
            IAppCompatCache appCache = null;
            OperatingSystem = OperatingSystemVersion.Unknown;

            string signature;

            //TODO check minimum length of rawBytes and throw exception if not enough data

            signature = Encoding.ASCII.GetString(rawBytes, 128, 4);

            if (signature == "\u0018\0\0\0" || signature == "Y\0\0\0")
            {
                OperatingSystem = OperatingSystemVersion.WindowsXP;
                appCache = new WindowsXP(rawBytes, is32, controlSet);
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
                else
                {
                    //win7
                    if (rawBytes[0] == 0xee & rawBytes[1] == 0xf & rawBytes[2] == 0xdc & rawBytes[3] == 0xba)
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
                }
            }

            

            Cache = appCache;
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
using System;
using System.IO;
using System.Linq;
using System.Text;
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

        //https://github.com/libyal/winreg-kb/wiki/Application-Compatibility-Cache-key
        //https://dl.mandiant.com/EE/library/Whitepaper_ShimCacheParser.pdf

        private void Init(byte[] rawBytes, bool is32)
        {
            IAppCompatCache appCache = null;
            OperatingSystem = OperatingSystemVersion.Unknown;

            string signature;

            //TODO check minimum length of rawBytes and throw exception if not enough data

            signature = Encoding.ASCII.GetString(rawBytes, 128, 4);

            if (signature == "\u0018\0\0\0" || signature == "Y\0\0\0")
            {

                OperatingSystem = OperatingSystemVersion.WindowsXP;
                appCache = new WindowsXP(rawBytes, is32);
            }
            else if ((signature == "00ts"))
            {
                OperatingSystem = OperatingSystemVersion.Windows80_Windows2012;
                appCache = new Windows8x(rawBytes, OperatingSystem);
            }
            else if (signature == "10ts")
            {
                OperatingSystem = OperatingSystemVersion.Windows81_Windows2012R2;
                appCache = new Windows8x(rawBytes, OperatingSystem);
            }
            else
            {
                //is it windows 10?
                signature = Encoding.ASCII.GetString(rawBytes, 48, 4);
                if ((signature == "10ts"))
                {
                    OperatingSystem = OperatingSystemVersion.Windows10;
                    appCache = new Windows10(rawBytes);
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

                        appCache = new Windows7(rawBytes, is32);
                    }
                }
            }

            Cache = appCache;
        }

        public AppCompatCache(byte[] rawBytes)
        {
           Init(rawBytes,false);
        }

        public AppCompatCache(string filename)
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
                if (File.Exists(filename) == false)
                {
                    throw new FileNotFoundException($"File not found ({filename})!");
                }

                var hive = new RegistryHiveOnDemand(filename);
                var subKey = hive.GetKey("Select");

                var currentCtlSet = int.Parse(subKey.Values.Single(c => c.ValueName == "Current").ValueData);

                subKey = hive.GetKey($@"ControlSet00{currentCtlSet}\Control\Session Manager\AppCompatCache");

                if (subKey == null)
                {
                    subKey = hive.GetKey($@"ControlSet00{currentCtlSet}\Control\Session Manager\AppCompatibility");
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

            Init(rawBytes, is32);
        }

        public IAppCompatCache Cache { get; private set; }
        public OperatingSystemVersion OperatingSystem { get; private set; }

        public static bool Is32Bit(string fileName)
        {
            if ((fileName.Length == 0))
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
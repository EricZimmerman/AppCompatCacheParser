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
            Windows7X86,
            Windows7X64,
            Windows80,
            Windows81Windows2012,
            Windows10,
            Unknown
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
                    Console.WriteLine(
                        @"'CurrentControlSet\Control\Session Manager\AppCompatCache' key not found! Exiting");
                    return;
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

            IAppCompatCache appCache = null;
            OperatingSystem = OperatingSystemVersion.Unknown;

            string signature;

            //TODO check minimum length of rawBytes and throw exception if not enough data

            signature = Encoding.ASCII.GetString(rawBytes, 128, 4);

            if ((signature == "00ts"))
            {
                OperatingSystem = OperatingSystemVersion.Windows80;
                appCache = new Windows8x(rawBytes, OperatingSystem);
            }
            else if (signature == "10ts")
            {
                OperatingSystem = OperatingSystemVersion.Windows81Windows2012;
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
                        var is32 = Is32Bit(filename);

                        if (is32)
                        {
                            OperatingSystem = OperatingSystemVersion.Windows7X86;
                        }
                        else
                        {
                            OperatingSystem = OperatingSystemVersion.Windows7X64;
                        }

                        appCache = new Windows7(rawBytes, is32);
                    }
                }
            }

            Cache = appCache;
        }

        public IAppCompatCache Cache { get; }
        public OperatingSystemVersion OperatingSystem { get; }

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
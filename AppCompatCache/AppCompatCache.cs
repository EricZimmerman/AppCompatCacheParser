using System;
using System.Linq;
using System.Text;
using Registry;

namespace AppCompatCache
{
    public class AppCompatCache
    {
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
                    Console.WriteLine(@"'CurrentControlSet\Control\Session Manager\AppCompatCache' key not found! Exiting");
                    return;
                }

                rawBytes = (byte[])subKey.GetValue("AppCompatCache", null);

            }
            else
            {
                var hive = new RegistryHiveOnDemand(filename);
                var subKey = hive.GetKey("Select");

                var currentCtlSet = int.Parse( subKey.Values.Single(c => c.ValueName == "Current").ValueData);

                subKey = hive.GetKey("ControlSet00" + currentCtlSet + @"\Control\Session Manager\AppCompatCache");

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
            OperatingSystem = "Unknown";

            var sig = string.Empty;

            //TODO check minimum length of rawBytes and throw exception if not enough data
            
sig = Encoding.ASCII.GetString(rawBytes, 128, 4);
           
               
            if ((sig == "00ts"))
            {
                // win 8.0
                OperatingSystem = "Windows 8.0";
                appCache = new Windows8x(rawBytes, "00ts");
            }
            else if (sig == "10ts")
            {
                // win 8.1
                OperatingSystem = "Windows 8.1";
                appCache = new Windows8x(rawBytes, "10ts");
            }
            else
            {
                //'is it windows 10?
                sig = Encoding.ASCII.GetString(rawBytes, 48, 4);
                if ((sig == "10ts"))
                {
                    OperatingSystem = "Windows 10";
                    appCache = new Windows10(rawBytes);
                }
                else
                {
                    //win7

                    if (rawBytes[0] == 0xee & rawBytes[1] == 0xf & rawBytes[2] == 0xdc & rawBytes[3] == 0xba)
                    {
                        var is32 = AppCompatCache.Is32Bit(filename);

                        if (is32)
                        {
                            OperatingSystem = "Windows 7 x86";
                        }
                        else
                        {
                            OperatingSystem = "Windows 7 x86";
                        }

                        appCache = new Windows7(rawBytes, is32);
                    }
                }

            }


            Cache = appCache;


        }

        public IAppCompatCache Cache { get; }

        public string OperatingSystem { get; }

        public static bool Is32Bit(string fileName)
        {
            if ((fileName.Length == 0))
            {
                var keyCurrUser = Microsoft.Win32.Registry.LocalMachine;
                var subKey = keyCurrUser.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");

                return subKey.GetValue("PROCESSOR_ARCHITECTURE").ToString().Equals("x86");
            }
            else
            {
                var hive = new RegistryHiveOnDemand(fileName);
                var subKey = hive.GetKey("Select");

                var currentCtlSet = int.Parse(subKey.Values.Single(c => c.ValueName == "Current").ValueData);

                subKey = hive.GetKey("ControlSet00" + currentCtlSet + @"\Control\Session Manager\Environment");

                if ((subKey != null))
                {
                    var val = subKey.Values.SingleOrDefault(c => c.ValueName == "PROCESSOR_ARCHITECTURE");

                    if (val != null)
                    {
                        return val.ValueData.Equals("x86");
                    }
                }
            }

            return true;
        }
    }
}
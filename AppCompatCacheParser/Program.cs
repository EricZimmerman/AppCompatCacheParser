using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AppCompatCache;
using CsvHelper;
using CsvHelper.Configuration;
using Fclp;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace AppCompatCacheParser
{
    internal class Program
    {
        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;

        private static void SetupNLog()
        {
            var config = new LoggingConfiguration();
            var loglevel = LogLevel.Info;

            var layout = @"${message}";

            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            consoleTarget.Layout = layout;

            var rule1 = new LoggingRule("*", loglevel, consoleTarget);
            config.LoggingRules.Add(rule1);

            LogManager.Configuration = config;
        }

        private static bool CheckForDotnet46()
        {
            using (
                var ndpKey =
                    RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                        .OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full\\"))
            {
                var releaseKey = Convert.ToInt32(ndpKey.GetValue("Release"));

                return releaseKey >= 393295;
            }
        }

        private static void Main(string[] args)
        {
            SetupNLog();

            var logger = LogManager.GetCurrentClassLogger();

            if (!CheckForDotnet46())
            {
                logger.Warn(".net 4.6 not detected. Please install .net 4.6 and try again.");
                return;
            }

            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>();

            _fluentCommandLineParser.Setup(arg => arg.SaveTo)
                .As('s')
                .WithDescription("(REQUIRED) Directory to save results")
                .Required();

            _fluentCommandLineParser.Setup(arg => arg.HiveFile)
                .As('h')
                .WithDescription(
                    "Full path to SYSTEM hive file to process. If this option is not specified, the live Registry will be used")
                .SetDefault(string.Empty);

            _fluentCommandLineParser.Setup(arg => arg.SortTimestamps)
                .As('t')
                .WithDescription("Sorts timestamps in descending order")
                .SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.ControlSet)
    .As('c')
    .WithDescription("The ControlSet to parse. Default is to detect the current control set.")
    .SetDefault(-1);

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying time stamps. Default is: yyyy-MM-dd HH:mm:ss K")
                .SetDefault("yyyy-MM-dd HH:mm:ss K");

            var header =
                $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}" +
                $"\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                $"\r\nhttps://github.com/EricZimmerman/AppCompatCacheParser";


            _fluentCommandLineParser.SetupHelp("?", "help").WithHeader(header).Callback(text => logger.Info(text));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

                logger.Info(
                    @"Example: AppCompatCacheParser.exe -s c:\temp -t -c 2");
                return;
            }

            var hiveToProcess = "Live Registry";

            if (_fluentCommandLineParser.Object.HiveFile?.Length > 0)
            {
                hiveToProcess = _fluentCommandLineParser.Object.HiveFile;
            }

            logger.Info(header);
            logger.Info("");

            logger.Info($"Processing hive '{hiveToProcess}'");

            logger.Info("");

            try
            {
                var appCompat = new AppCompatCache.AppCompatCache(_fluentCommandLineParser.Object.HiveFile, _fluentCommandLineParser.Object.ControlSet);

                var outFileBase = string.Empty;

                if (_fluentCommandLineParser.Object.HiveFile?.Length > 0)
                {
                    if (_fluentCommandLineParser.Object.ControlSet >= 0)
                    {
                        outFileBase =
                        $"{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(_fluentCommandLineParser.Object.HiveFile)}_ControlSet00{_fluentCommandLineParser.Object.ControlSet}_AppCompatCache.tsv";
                    }
                    else
                    {
                        outFileBase =
                            $"{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(_fluentCommandLineParser.Object.HiveFile)}_AppCompatCache.tsv";
                    }
                    
                }
                else
                {
                    outFileBase = $"{appCompat.OperatingSystem}_{Environment.MachineName}_AppCompatCache.tsv";
                }

                if (Directory.Exists(_fluentCommandLineParser.Object.SaveTo) == false)
                {
                    Directory.CreateDirectory(_fluentCommandLineParser.Object.SaveTo);
                }

                var outFilename = Path.Combine(_fluentCommandLineParser.Object.SaveTo, outFileBase);

                logger.Info($"\r\nResults will be saved to '{outFilename}'\r\n");

                var sw = new StreamWriter(outFilename);
                sw.AutoFlush = true;
                var csv = new CsvWriter(sw);

                csv.Configuration.RegisterClassMap(new CacheOutputMap(_fluentCommandLineParser.Object.DateTimeFormat));
                csv.Configuration.Delimiter = "\t";

                csv.WriteHeader<CacheEntry>();

                if (appCompat.Caches.Any())
                {
                    foreach (var appCompatCach in appCompat.Caches)
                    {
                        if (appCompatCach.ControlSet == -1)
                        {
                            logger.Info(
                        $"Found {appCompatCach.Entries.Count:N0} cache entries for {appCompat.OperatingSystem} in CurrentControlSet");
                        }
                        else
                        {
                            logger.Info(
                            $"Found {appCompatCach.Entries.Count:N0} cache entries for {appCompat.OperatingSystem} in ControlSet00{appCompatCach.ControlSet}");
                        }
                        

                       

                        if (_fluentCommandLineParser.Object.SortTimestamps)
                        {
                            csv.WriteRecords(appCompatCach.Entries.OrderByDescending(t => t.LastModifiedTimeUTC));
                        }
                        else
                        {
                            csv.WriteRecords(appCompatCach.Entries);
                        }


                     
                    }

                    sw.Close();

                }
            }
            catch (Exception ex)
            {
                logger.Error($"There was an error: Error message: {ex.Message}");
            }


#if DEBUG
            logger.Info("");
            logger.Info("Press a key to exit");
            Console.ReadKey();
#endif
        }
    }

    public class ApplicationArguments
    {
        public string HiveFile { get; set; }
        public bool FindEvidence { get; set; }
        public bool SortTimestamps { get; set; }
        public int ControlSet { get; set; }
        public string SaveTo { get; set; }

        public string DateTimeFormat { get; set; }
    }

    public sealed class CacheOutputMap : CsvClassMap<CacheEntry>
    {
        public CacheOutputMap(string dateformat)
        {
            Map(m => m.ControlSet);
            Map(m => m.CacheEntryPosition);
            Map(m => m.Path);
            Map(m => m.LastModifiedTimeUTC).TypeConverterOption(dateformat);
            Map(m => m.Executed);
        }
    }
}
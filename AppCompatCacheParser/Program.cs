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

            var p = new FluentCommandLineParser<ApplicationArguments>();

            p.Setup(arg => arg.SaveTo)
                .As('s')
                .WithDescription("(REQUIRED) Directory to save results")
                .Required();

            p.Setup(arg => arg.HiveFile)
                .As('h')
                .WithDescription(
                    "Full path to SYSTEM hive file to process. If this option is not specified, the live Registry will be used")
                .SetDefault(string.Empty);

            p.Setup(arg => arg.SortTimestamps)
                .As('t')
                .WithDescription("If true, sorts timestamps in descending order")
                .SetDefault(false);

            var header =
                $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}" +
                $"\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                $"\r\nhttps://github.com/EricZimmerman/AppCompatCacheParser";


            p.SetupHelp("?", "help").WithHeader(header).Callback(text => logger.Info(text));

            var result = p.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                p.HelpOption.ShowHelp(p.Options);

                logger.Info(
                    @"Example: AppCompatCacheParser.exe -s c:\temp -t");
                return;
            }

            var hiveToProcess = "Live Registry";

            if (p.Object.HiveFile?.Length > 0)
            {
                hiveToProcess = p.Object.HiveFile;
            }

            logger.Info(header);
            logger.Info("");

            logger.Info($"Processing hive '{hiveToProcess}'");

            logger.Info("");

            try
            {
                var appCompat = new AppCompatCache.AppCompatCache(p.Object.HiveFile);

                if (appCompat.Cache != null)
                {
                    logger.Info(
                        $"Found {appCompat.Cache.Entries.Count:N0} cache entries for {appCompat.OperatingSystem}");

                    var outFileBase = string.Empty;

                    if (p.Object.HiveFile?.Length > 0)
                    {
                        outFileBase =
                            $"{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(p.Object.HiveFile)}_AppCompatCache.tsv";
                    }
                    else
                    {
                        outFileBase = $"{appCompat.OperatingSystem}_{Environment.MachineName}_AppCompatCache.tsv";
                    }

                    if (Directory.Exists(p.Object.SaveTo) == false)
                    {
                        Directory.CreateDirectory(p.Object.SaveTo);
                    }

                    var outFilename = Path.Combine(p.Object.SaveTo, outFileBase);

                    logger.Info($"\r\nSaving results to '{outFilename}'");

                    var sw = new StreamWriter(outFilename);
                    sw.AutoFlush = true;
                    var csv = new CsvWriter(sw);

                    csv.Configuration.RegisterClassMap<CacheOutputMap>();
                    csv.Configuration.Delimiter = "\t";
                    //csv.Configuration.AllowComments = true;

                    csv.WriteHeader<CacheEntry>();

                    if (p.Object.SortTimestamps)
                    {
                        csv.WriteRecords(appCompat.Cache.Entries.OrderByDescending(t => t.LastModifiedTimeUTC));
                    }
                    else
                    {
                        csv.WriteRecords(appCompat.Cache.Entries);
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
        public string SaveTo { get; set; }
    }

    public sealed class CacheOutputMap : CsvClassMap<CacheEntry>
    {
        public CacheOutputMap()
        {
            Map(m => m.CacheEntryPosition);
            Map(m => m.Path);
            Map(m => m.LastModifiedTimeUTC).TypeConverterOption("MM-dd-yyyy HH:mm:ss");
        }
    }
}
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using AppCompatCache;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Exceptionless;
using Fclp;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;

namespace AppCompatCacheParser
{
    internal class Program
    {
        private static FluentCommandLineParser<ApplicationArguments> _fluentCommandLineParser;
        private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static void SetupNLog()
        {
            if (File.Exists( Path.Combine(BaseDirectory,"Nlog.config")))
            {
                return;
            }
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

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }


        private static void Main(string[] args)
        {
            ExceptionlessClient.Default.Startup("7iL4b0Me7W8PbFflftqWgfQCIdf55flrT2O11zIP");
            SetupNLog();

            var logger = LogManager.GetCurrentClassLogger();



            _fluentCommandLineParser = new FluentCommandLineParser<ApplicationArguments>();

            _fluentCommandLineParser.Setup(arg => arg.CsvDirectory)
                .As("csv")
                .WithDescription("Directory to save CSV formatted results to. Required")
                .Required();
            _fluentCommandLineParser.Setup(arg => arg.CsvName)
                .As("csvf")
                .WithDescription("File name to save CSV formatted results to. When present, overrides default name\r\n");
                

            _fluentCommandLineParser.Setup(arg => arg.HiveFile)
                .As('f')
                .WithDescription(
                    "Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used")
                .SetDefault(string.Empty);

            _fluentCommandLineParser.Setup(arg => arg.SortTimestamps)
                .As('t')
                .WithDescription("Sorts last modified timestamps in descending order\r\n")
                .SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.ControlSet)
                .As('c')
                .WithDescription("The ControlSet to parse. Default is to extract all control sets.")
                .SetDefault(-1);

            _fluentCommandLineParser.Setup(arg => arg.Debug)
                .As('d')
                .WithDescription("Debug mode")
                .SetDefault(false);

            _fluentCommandLineParser.Setup(arg => arg.DateTimeFormat)
                .As("dt")
                .WithDescription(
                    "The custom date/time format to use when displaying timestamps. See https://goo.gl/CNVq0k for options. Default is: yyyy-MM-dd HH:mm:ss")
                .SetDefault("yyyy-MM-dd HH:mm:ss");


            _fluentCommandLineParser.Setup(arg => arg.NoTransLogs)
                .As("nl")
                .WithDescription(
                    "When true, ignore transaction log files for dirty hives. Default is FALSE").SetDefault(false);

            var header =
                $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}" +
                $"\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
                $"\r\nhttps://github.com/EricZimmerman/AppCompatCacheParser";

            var footer = @"Examples: AppCompatCacheParser.exe --csv c:\temp -t -c 2" + "\r\n\t " +
                         @" AppCompatCacheParser.exe --csv c:\temp --csvf results.csv" + "\r\n\t " +
                         "\r\n\t" +
                         "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";


            _fluentCommandLineParser.SetupHelp("?", "help").WithHeader(header).Callback(text => logger.Info(text + "\r\n" + footer));

            var result = _fluentCommandLineParser.Parse(args);

            if (result.HelpCalled)
            {
                return;
            }

            if (result.HasErrors)
            {
                _fluentCommandLineParser.HelpOption.ShowHelp(_fluentCommandLineParser.Options);

        
                return;
            }

            var hiveToProcess = "Live Registry";

            if (_fluentCommandLineParser.Object.HiveFile?.Length > 0)
            {
                hiveToProcess = _fluentCommandLineParser.Object.HiveFile;
            }

            logger.Info(header);
            logger.Info("");
            logger.Info($"Command line: {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}\r\n");

            if (IsAdministrator() == false)
            {
                logger.Fatal($"Warning: Administrator privileges not found!\r\n");
            }

            logger.Info($"Processing hive '{hiveToProcess}'");

            logger.Info("");

            if (_fluentCommandLineParser.Object.Debug)
            {
                LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
            }

            try
            {
                var appCompat = new AppCompatCache.AppCompatCache(_fluentCommandLineParser.Object.HiveFile,
                    _fluentCommandLineParser.Object.ControlSet,_fluentCommandLineParser.Object.NoTransLogs);

                var outFileBase = string.Empty;
                var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

                if (_fluentCommandLineParser.Object.HiveFile?.Length > 0)
                {
                    if (_fluentCommandLineParser.Object.ControlSet >= 0)
                    {
                        outFileBase =
                            $"{ts1}_{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(_fluentCommandLineParser.Object.HiveFile)}_ControlSet00{_fluentCommandLineParser.Object.ControlSet}_AppCompatCache.csv";
                    }
                    else
                    {
                        outFileBase =
                            $"{ts1}_{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(_fluentCommandLineParser.Object.HiveFile)}_AppCompatCache.csv";
                    }
                }
                else
                {
                    outFileBase = $"{ts1}_{appCompat.OperatingSystem}_{Environment.MachineName}_AppCompatCache.csv";
                }

                if (_fluentCommandLineParser.Object.CsvName.IsNullOrEmpty() == false)
                {
                    outFileBase = Path.GetFileName(_fluentCommandLineParser.Object.CsvName);
                }

                if (Directory.Exists(_fluentCommandLineParser.Object.CsvDirectory) == false)
                {
                    Directory.CreateDirectory(_fluentCommandLineParser.Object.CsvDirectory);
                }

                var outFilename = Path.Combine(_fluentCommandLineParser.Object.CsvDirectory, outFileBase);

                var sw = new StreamWriter(outFilename);
            
                var csv = new CsvWriter(sw);
                csv.Configuration.HasHeaderRecord = true;
                

                var foo = csv.Configuration.AutoMap<CacheEntry>();
                var o = new TypeConverterOptions
                {
                    DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
                };
                csv.Configuration.TypeConverterOptionsCache.AddOptions<CacheEntry>(o);

                foo.Map(t => t.LastModifiedTimeUTC).ConvertUsing(t=>t.LastModifiedTimeUTC.ToString(_fluentCommandLineParser.Object.DateTimeFormat));

                foo.Map(t => t.CacheEntrySize).Ignore();
                foo.Map(t => t.Data).Ignore();
                foo.Map(t => t.InsertFlags).Ignore();
                foo.Map(t => t.DataSize).Ignore();
                foo.Map(t => t.LastModifiedFILETIMEUTC).Ignore();
                foo.Map(t => t.PathSize).Ignore();
                foo.Map(t => t.Signature).Ignore();

                foo.Map(t => t.ControlSet).Index(0);
                foo.Map(t => t.CacheEntryPosition).Index(1);
                foo.Map(t => t.Path).Index(2);
                foo.Map(t => t.LastModifiedTimeUTC).Index(3);
                foo.Map(t => t.Executed).Index(4);

                csv.WriteHeader<CacheEntry>();
                csv.NextRecord();

                logger.Debug($"**** Found {appCompat.Caches.Count} caches");

                if (appCompat.Caches.Any())
                {
                    foreach (var appCompatCach in appCompat.Caches)
                    {
                        if (_fluentCommandLineParser.Object.Debug)
                        {
                            appCompatCach.PrintDump();
                        }
                        
                        try
                        {
                            logger.Info(
                                $"Found {appCompatCach.Entries.Count:N0} cache entries for {appCompat.OperatingSystem} in ControlSet00{appCompatCach.ControlSet}");

                            if (_fluentCommandLineParser.Object.SortTimestamps)
                            {
                                csv.WriteRecords(appCompatCach.Entries.OrderByDescending(t => t.LastModifiedTimeUTC));
                            }
                            else
                            {
                                csv.WriteRecords(appCompatCach.Entries);
                            }
                        }
                        catch (Exception ex)
                        {
                                logger.Error($"There was an error: Error message: {ex.Message} Stack: {ex.StackTrace}");

                            try
                            {
                                appCompatCach.PrintDump();
                            }
                            catch (Exception ex1)
                            {
                                logger.Error($"Couldn't PrintDump {ex1.Message} Stack: {ex1.StackTrace}");

                            }
                        }
                    }
                    sw.Flush();
                    sw.Close();

                    logger.Warn($"\r\nResults saved to '{outFilename}'\r\n");
                }
                else
                {
                    logger.Warn($"\r\nNo caches were found!\r\n");
                }
                
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Sequence numbers do not match and transaction logs were not found in the same direct") == false)
                {
                    logger.Error($"There was an error: Error message: {ex.Message} Stack: {ex.StackTrace}");
                }
                
            }
        }
    }

    public class ApplicationArguments
    {
        public string HiveFile { get; set; }
        public bool SortTimestamps { get; set; }
        public int ControlSet { get; set; }
        public string CsvDirectory { get; set; }
        public string CsvName { get; set; }

        public bool Debug { get; set; }

        public bool NoTransLogs { get; set; } = false;

        public string DateTimeFormat { get; set; }

    }

}
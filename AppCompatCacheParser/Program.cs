using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using AppCompatCache;
using CsvHelper.TypeConversion;
using Exceptionless;
using NLog;
using NLog.Config;
using NLog.Targets;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;

namespace AppCompatCacheParser;

internal class Program
{
    private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private static string[] _args;
        
    private static readonly string Header =
        $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}" +
        $"\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        $"\r\nhttps://github.com/EricZimmerman/AppCompatCacheParser";

    private static readonly string Footer = @"Examples: AppCompatCacheParser.exe --csv c:\temp -t -c 2" + "\r\n\t " +
                                            @" AppCompatCacheParser.exe --csv c:\temp --csvf results.csv" + "\r\n\t " +
                                            "\r\n\t" +
                                            "  Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

    private static RootCommand _rootCommand;


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

    private static bool IsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return true;
        }
            
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }


    private static async Task Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("7iL4b0Me7W8PbFflftqWgfQCIdf55flrT2O11zIP");
        SetupNLog();

        _args = args;
        
        var csvOption = new Option<string>(
            "--csv",
            "Directory to save CSV formatted results to. Be sure to include the full path in double quotes");
        csvOption.IsRequired = true;
            
        _rootCommand = new RootCommand
        {
            new Option<string>(
                "-f",
                "Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used"),
                
            csvOption,
                
            new Option<string>(
                "--csvf",
                "File name to save CSV formatted results to. When present, overrides default name\r\n"),
                
            new Option<int>(
                "--c",
                getDefaultValue:()=>-1,
                "The ControlSet to parse. Default is to extract all control sets"),
                
            new Option<bool>(
                "-t",
                getDefaultValue:()=>false,
                description: "Sorts last modified timestamps in descending order\r\n"),
                
            new Option<string>(
                "--dt",
                getDefaultValue:()=>"yyyy-MM-dd HH:mm:ss",
                "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options"),
            
            new Option<bool>(
                "--nl",
                getDefaultValue:()=>false,
                "When true, ignore transaction log files for dirty hives. Default is FALSE\r\n"),
            
            new Option<bool>(
                "--debug",
                getDefaultValue:()=>false,
                "Show debug information during processing"),
            
            new Option<bool>(
                "--trace",
                getDefaultValue:()=>false,
                "Show trace information during processing"),
                
        };
            
        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.Handler = System.CommandLine.NamingConventionBinder.CommandHandler.Create<string,string,string,int, bool,string,bool,bool,bool>(DoWork);
            
        await _rootCommand.InvokeAsync(args);
    }

    private static void DoWork(string f,string csv,string csvf,int c, bool t,string dt,bool nl,bool debug,bool trace)
    {
        var logger = LogManager.GetCurrentClassLogger();
        var hiveToProcess = "Live Registry";
        
        if (f?.Length > 0)
        {
            hiveToProcess = f;
            if (!File.Exists(f))
            {
                logger.Warn($"'{f}' not found. Exiting");
                return;
            }
        }
        else
        {
            
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("Live Registry support is not available on non-Windows systems");
            }
        }

        logger.Info(Header);
        logger.Info("");
        logger.Info($"Command line: {string.Join(" ", _args)}\r\n");

        if (IsAdministrator() == false)
        {
            logger.Fatal($"Warning: Administrator privileges not found!\r\n");
        }

        logger.Info($"Processing hive '{hiveToProcess}'");

        logger.Info("");

        if (debug)
        {
            LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Debug);
        }
            
        if (trace)
        {
            LogManager.Configuration.LoggingRules.First().EnableLoggingForLevel(LogLevel.Trace);
        }
            
        LogManager.ReconfigExistingLoggers();

        try
        {
            var appCompat = new AppCompatCache.AppCompatCache(f,
                c,nl);

            string outFileBase;
            var ts1 = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (f?.Length > 0)
            {
                if (c >= 0)
                {
                    outFileBase =
                        $"{ts1}_{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(f)}_ControlSet00{c}_AppCompatCache.csv";
                }
                else
                {
                    outFileBase =
                        $"{ts1}_{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(f)}_AppCompatCache.csv";
                }
            }
            else
            {
                outFileBase = $"{ts1}_{appCompat.OperatingSystem}_{Environment.MachineName}_AppCompatCache.csv";
            }

            if (csvf.IsNullOrEmpty() == false)
            {
                outFileBase = Path.GetFileName(csvf);
            }

            if (Directory.Exists(csv) == false)
            {
                Directory.CreateDirectory(csv!);
            }

            var outFilename = Path.Combine(csv, outFileBase);

            var sw = new StreamWriter(outFilename);
            
            var csvWriter = new CsvWriter(sw,CultureInfo.InvariantCulture);

            var foo = csvWriter.Context.AutoMap<CacheEntry>();
            var o = new TypeConverterOptions
            {
                DateTimeStyle = DateTimeStyles.AssumeUniversal & DateTimeStyles.AdjustToUniversal
            };
            csvWriter.Context.TypeConverterOptionsCache.AddOptions<CacheEntry>(o);

            foo.Map(entry => entry.LastModifiedTimeUTC).Convert(entry=>entry.Value.LastModifiedTimeUTC.HasValue ? entry.Value.LastModifiedTimeUTC.Value.ToString(dt): "");

            foo.Map(entry => entry.CacheEntrySize).Ignore();
            foo.Map(entry => entry.Data).Ignore();
            foo.Map(entry => entry.InsertFlags).Ignore();
            foo.Map(entry => entry.DataSize).Ignore();
            foo.Map(entry => entry.LastModifiedFILETIMEUTC).Ignore();
            foo.Map(entry => entry.PathSize).Ignore();
            foo.Map(entry => entry.Signature).Ignore();

            foo.Map(entry => entry.ControlSet).Index(0);
            foo.Map(entry => entry.CacheEntryPosition).Index(1);
            foo.Map(entry => entry.Path).Index(2);
            foo.Map(entry => entry.LastModifiedTimeUTC).Index(3);
            foo.Map(entry => entry.Executed).Index(4);
            foo.Map(entry => entry.Duplicate).Index(5);
            foo.Map(entry => entry.SourceFile).Index(6);

            csvWriter.WriteHeader<CacheEntry>();
            csvWriter.NextRecord();

            logger.Debug($"**** Found {appCompat.Caches.Count} caches");

            var cacheKeys = new HashSet<string>();

            if (appCompat.Caches.Any())
            {
                foreach (var appCompatCach in appCompat.Caches)
                {
                    if (debug)
                    {
                        appCompatCach.PrintDump();
                    }
                        
                    try
                    {
                        logger.Info(
                            $"Found {appCompatCach.Entries.Count:N0} cache entries for {appCompat.OperatingSystem} in ControlSet00{appCompatCach.ControlSet}");

                        if (t)
                        {
                            foreach (var cacheEntry in appCompatCach.Entries)
                            {
                                cacheEntry.SourceFile = hiveToProcess;
                                cacheEntry.Duplicate = cacheKeys.Contains(cacheEntry.GetKey());

                                cacheKeys.Add(cacheEntry.GetKey());

                                csvWriter.WriteRecord(cacheEntry);
                                csvWriter.NextRecord();
                            }

                        }
                        else
                        {
                            foreach (var cacheEntry in appCompatCach.Entries)
                            {
                                cacheEntry.SourceFile = hiveToProcess;
                                cacheEntry.Duplicate = cacheKeys.Contains(cacheEntry.GetKey());

                                cacheKeys.Add(cacheEntry.GetKey());

                                csvWriter.WriteRecord(cacheEntry);
                                csvWriter.NextRecord();
                            }
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
                if (ex.Message.Contains("Administrator privileges not found"))
                {
                    logger.Fatal($"Could not access '{f}'. Does it exist?");
                    logger.Error("");
                    logger.Fatal("Rerun the program with Administrator privileges to try again\r\n");
                }
                else if (ex.Message.Contains("Invalid diskName:"))
                {
                    logger.Fatal($"Could not access '{f}'. Invalid disk!");
                    logger.Error("");
                }
                else
                {
                    logger.Error($"There was an error: {ex.Message}");
                    logger.Error($"Stacktrace: {ex.StackTrace}");
                    logger.Info("");
                }

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
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
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ServiceStack;
using ServiceStack.Text;
using CsvWriter = CsvHelper.CsvWriter;

namespace AppCompatCacheParser;

internal class Program
{
    //private static readonly string BaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private static string[] _args;
        
    private static readonly string Header =
        $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}" +
        $"\r\n\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)" +
        $"\r\nhttps://github.com/EricZimmerman/AppCompatCacheParser";

    private static readonly string Footer = @"Examples: AppCompatCacheParser.exe --csv c:\temp -t -c 2" + "\r\n\t " +
                                            @"   AppCompatCacheParser.exe --csv c:\temp --csvf results.csv" + "\r\n\t " +
                                            "\r\n\t" +
                                            "    Short options (single letter) are prefixed with a single dash. Long commands are prefixed with two dashes\r\n";

    private static RootCommand _rootCommand;


    
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
                "File name to save CSV formatted results to. When present, overrides default name"),
                
            new Option<int>(
                "--c",
                getDefaultValue:()=>-1,
                "The ControlSet to parse. Default is to extract all control sets"),
                
            new Option<bool>(
                "-t",
                getDefaultValue:()=>false,
                description: "Sorts last modified timestamps in descending order"),
                
            new Option<string>(
                "--dt",
                getDefaultValue:()=>"yyyy-MM-dd HH:mm:ss",
                "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options"),
            
            new Option<bool>(
                "--nl",
                getDefaultValue:()=>false,
                "When true, ignore transaction log files for dirty hives"),
            
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
        
        Log.CloseAndFlush();
    }

    private static void DoWork(string f,string csv,string csvf,int c, bool t,string dt,bool nl,bool debug,bool trace)
    {
        var levelSwitch = new LoggingLevelSwitch();

        var template = "{Message:lj}{NewLine}{Exception}";

        if (debug)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Debug;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }

        if (trace)
        {
            levelSwitch.MinimumLevel = LogEventLevel.Verbose;
            template = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}";
        }
        
        var conf = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: template)
            .MinimumLevel.ControlledBy(levelSwitch);
      
        Log.Logger = conf.CreateLogger();
        
        var hiveToProcess = "Live Registry";
        
        if (f?.Length > 0)
        {
            hiveToProcess = f;
            if (!File.Exists(f))
            {
                Log.Warning("'{F}' not found. Exiting",f);
                return;
            }
        }
        else
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.Fatal("Live Registry support is not available on non-Windows systems");
                Environment.Exit(0);
                //throw new NotSupportedException("Live Registry support is not available on non-Windows systems");
            }
        }

        Log.Information("{Header}",Header);
        Console.WriteLine();
        Log.Information("Command line: {Args}",string.Join(" ", _args));
        Console.WriteLine();

        if (IsAdministrator() == false)
        {
            Log.Fatal($"Warning: Administrator privileges not found!");
            Console.WriteLine();
        }

        Log.Information("Processing hive '{HiveToProcess}'",hiveToProcess);

        Console.WriteLine();

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

            Log.Debug("**** Found {Count} caches",appCompat.Caches.Count);

            var cacheKeys = new HashSet<string>();

            if (appCompat.Caches.Any())
            {
                foreach (var appCompatCach in appCompat.Caches)
                {
                  
                  Log.Verbose("Dumping cache details: {@Details}",appCompat);
                  
                    try
                    {
                        Log.Information(
                            "Found {Count:N0} cache entries for {OperatingSystem} in {ControlSet}",appCompatCach.Entries.Count,appCompat.OperatingSystem,$"ControlSet00{appCompatCach.ControlSet}");

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
                        Log.Error(ex,"There was an error: Error message: {Message}",ex.Message);

                        try
                        {
                            appCompatCach.PrintDump();
                        }
                        catch (Exception ex1)
                        {
                            Log.Error(ex1,"Couldn't PrintDump: {Message}",ex1.Message);
                        }
                    }
                }
                sw.Flush();
                sw.Close();

                Console.WriteLine();
                Log.Warning("Results saved to '{OutFilename}'",outFilename);
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Log.Warning("No caches were found!");
                Console.WriteLine();
            }
                
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Sequence numbers do not match and transaction logs were not found in the same direct") == false)
            {
                if (ex.Message.Contains("Administrator privileges not found"))
                {
                    Log.Fatal("Could not access '{F}'. Does it exist?",f);
                    Console.WriteLine();
                    Log.Fatal("Rerun the program with Administrator privileges to try again");
                    Console.WriteLine();
                }
                else if (ex.Message.Contains("Invalid diskName:"))
                {
                    Log.Fatal("Could not access '{F}'. Invalid disk!",f);
                    Console.WriteLine();
                }
                else
                {
                    Log.Error(ex,"There was an error: {Message}",ex.Message);
                    Console.WriteLine();
                }

            }
                
        }
    }

}

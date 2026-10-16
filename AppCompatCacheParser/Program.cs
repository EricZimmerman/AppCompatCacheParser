using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
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
        $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}" +
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


    private static async Task<int> Main(string[] args)
    {
        ExceptionlessClient.Default.Startup("7iL4b0Me7W8PbFflftqWgfQCIdf55flrT2O11zIP");
    
        _args = args;
   
        var fOpt = new Option<string>("-f")
        {
            Description = "Full path to SYSTEM hive to process. If this option is not specified, the live Registry will be used"
        };
        
        var csvOpt = new Option<string>(
            "--csv")
        {
            Description = "Directory to save CSV formatted results to. Be sure to include the full path in double quotes"
        
        };

        var csvfOpt = new Option<string>(
            "--csvf")
        {
            Description   = "File name to save CSV formatted results to. When present, overrides default name\r\n"
        };
        
          
        var cOpt = new Option<int>("-c")
        {
            Description = "The ControlSet to parse. Default is to extract all control sets",
            DefaultValueFactory = _ => -1
        };
          
        var tOpt = new Option<bool>("-t")
        {
            Description = "Sorts last modified timestamps in descending order",
            DefaultValueFactory = _ => false
        };
        
        var dtOpt = new Option<string>(
            "--dt"){
            Description =        "The custom date/time format to use when displaying time stamps. See https://goo.gl/CNVq0k for options",
            DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss"
        };
        
        var nlOpt = new Option<bool>("--nl")
        {
            Description = "When true, ignore transaction log files for dirty hives",
            DefaultValueFactory = _ => false
        };
        
        
        var debugOpt = new Option<bool>("--debug")
        {
            Description = "Show debug information during processing",
            DefaultValueFactory = _ => false
        };
        var traceOpt = new Option<bool>("--trace")
        {
            Description = "Show trace information during processing",
            DefaultValueFactory = _ => false
        };
            
        _rootCommand = new RootCommand
        {
           fOpt,
           csvOpt,
           csvfOpt,
           cOpt,
           tOpt,
          dtOpt,
          nlOpt,
          debugOpt,
          traceOpt
          };
            
        _rootCommand.Description = Header + "\r\n\r\n" + Footer;

        _rootCommand.SetAction(result => DoWork(result.GetValue(fOpt), result.GetValue(csvOpt), result.GetValue(csvfOpt),
            result.GetValue(cOpt), result.GetValue(tOpt), result.GetValue(dtOpt), result.GetValue(nlOpt),
            result.GetValue(debugOpt),result.GetValue(traceOpt)));
            

        var exitCode = await _rootCommand.Parse(args).InvokeAsync();

        Log.CloseAndFlush();

        return exitCode;
    }

    /// <returns>0 when the hive was processed, 1 when it could not be (or no cache was found, or the CSV could not be written)</returns>
    private static int DoWork(string f,string csv,string csvf,int c, bool t,string dt,bool nl,bool debug,bool trace)
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
        
        if (string.IsNullOrEmpty(csv))
        {
            var aaa = new CustomHelpAction(new HelpAction());
            aaa.Invoke(_rootCommand.Parse("--csv is required. Exiting"));

            return 1;
        }

        
        var hiveToProcess = "Live Registry";
        
        if (f?.Length > 0)
        {
            hiveToProcess = f;
            if (!File.Exists(f))
            {
                Log.Warning("'{F}' not found. Exiting",f);
                return 1;
            }
        }
        else
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.Fatal("Live Registry support is not available on non-Windows systems");
                return 1;
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

            Log.Debug("**** Found {Count} caches",appCompat.Caches.Count);

            if (appCompat.Caches.Any() == false)
            {
                Console.WriteLine();
                Log.Warning("No caches were found!");
                Console.WriteLine();
                return 1;
            }

            using (var sw = new StreamWriter(outFilename))
            {
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
                foo.Map(entry => entry.LastUpdateTimeUTC).Ignore();
                foo.Map(entry => entry.PathSize).Ignore();
                foo.Map(entry => entry.Signature).Ignore();

                foo.Map(entry => entry.ControlSet).Index(0);
                foo.Map(entry => entry.CacheEntryPosition).Index(1);
                foo.Map(entry => entry.Path).Index(2);
                foo.Map(entry => entry.LastModifiedTimeUTC).Index(3);
                foo.Map(entry => entry.Executed).Index(4);
                foo.Map(entry => entry.Duplicate).Index(5);
                foo.Map(entry => entry.SourceFile).Index(6);
                foo.Map(entry => entry.FromSlack).Index(7);

                csvWriter.WriteHeader<CacheEntry>();
                csvWriter.NextRecord();

                var cacheKeys = new HashSet<string>();

                foreach (var appCompatCach in appCompat.Caches)
                {
                    Log.Verbose("Dumping cache details: {@Details}",appCompat);

                    Log.Information(
                        "Found {Count:N0} cache entries for {OperatingSystem} in {ControlSet}",appCompatCach.Entries.Count,appCompat.OperatingSystem,$"ControlSet00{appCompatCach.ControlSet}");

                    foreach (var parseError in appCompatCach.ParseErrors)
                    {
                        Log.Warning("ControlSet00{ControlSet}: {Error}",appCompatCach.ControlSet,parseError);
                    }

                    if (appCompatCach.SlackEntries.Count > 0)
                    {
                        Log.Information("Recovered {Count:N0} entries that are no longer in the cache from its value's slack in {ControlSet}",appCompatCach.SlackEntries.Count,$"ControlSet00{appCompatCach.ControlSet}");
                    }

                    //-t: newest last modified time first (entries without one last); the cache's own order otherwise. Entries
                    //from slack come after the cache's, in the same order
                    IEnumerable<CacheEntry> Sorted(List<CacheEntry> list) => t
                        ? list.OrderByDescending(e => e.LastModifiedTimeUTC.HasValue).ThenByDescending(e => e.LastModifiedTimeUTC)
                        : list;

                    var entries = Sorted(appCompatCach.Entries).Concat(Sorted(appCompatCach.SlackEntries));

                    foreach (var cacheEntry in entries)
                    {
                        cacheEntry.SourceFile = hiveToProcess;
                        cacheEntry.Duplicate = cacheKeys.Contains(cacheEntry.GetKey());

                        cacheKeys.Add(cacheEntry.GetKey());

                        csvWriter.WriteRecord(cacheEntry);
                        csvWriter.NextRecord();
                    }
                }

                sw.Flush();
            }

            Console.WriteLine();
            Log.Warning("Results saved to '{OutFilename}'",outFilename);
            Console.WriteLine();

            return 0;
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

                else if (ex.Message.Contains("Could not find ControlSet"))
                {
                    Console.WriteLine();
                    Log.Fatal("{Msg}. Exiting",ex.Message);
                    Console.WriteLine();
                }
                else if (ex is AppCompatCacheParseException)
                {
                    Console.WriteLine();
                    Log.Error("{Message}",ex.Message);
                    Console.WriteLine();
                }
                else if (ex.Message.Contains("Invalid diskName:"))
                {
                    Console.WriteLine();
                    Log.Fatal("Could not access '{F}'. Invalid disk!",f);
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                    Log.Error(ex,"There was an error: {Message}",ex.Message);
                    Console.WriteLine();
                }

            }

            return 1;
        }
    }

    private class CustomHelpAction : SynchronousCommandLineAction
    {
        private readonly HelpAction _defaultHelp;

        public CustomHelpAction(HelpAction action)
        {
            _defaultHelp = action;
        }

        public override int Invoke(ParseResult parseResult)
        {
            var result = _defaultHelp.Invoke(parseResult);

            Log.Warning("{Msg}", string.Join(" ",parseResult.Tokens));

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AppCompatCache;
using CsvHelper;
using CsvHelper.Configuration;
using Fclp;
using Microsoft.Win32;

namespace AppCompatCacheParser
{
    class Program
    {
        static void Main(string[] args)
        {

            //http://joshclose.github.io/CsvHelper/ ??

            var logger = NLog.LogManager.GetCurrentClassLogger();

            var p = new FluentCommandLineParser<ApplicationArguments>();

            p.Setup(arg => arg.HiveFile).As('h',"HiveFile").WithDescription("Full path to hive file to process. If this option is not specified, the live Registry will be processed").SetDefault(string.Empty);
            p.Setup(arg => arg.SaveTo).As('s',"SaveTo").WithDescription("Directory to save results to (REQUIRED)").Required();
            p.Setup(arg => arg.FindEvidence).As('f',"FindEvidence").WithDescription("Be careful what you ask for").SetDefault(false);

            var header =
                $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)";
            p.SetupHelp("?", "help").WithHeader(header).Callback(text => logger.Info(text));

            var result =  p.Parse(args);

            if (result.HasErrors)
            {
                p.HelpOption.ShowHelp(p.Options);
                return;
            }

            if (p.Object.FindEvidence)
            {
                logger.Info("\r\nThis is not the forensics program you are looking for...");
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


                if ((appCompat.Cache != null))
                {
                    logger.Info($"Found {appCompat.Cache.Entries.Count:N0} cache entries for {appCompat.OperatingSystem}");


                    var outFileBase = string.Empty;

                    if (p.Object.HiveFile?.Length > 0)
                    {
                        outFileBase = $"{appCompat.OperatingSystem}_{Path.GetFileNameWithoutExtension(p.Object.HiveFile)}_AppCompatCache.tsv";
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

                    logger.Info($"Saving results to '{outFilename}'");

                    var sw = new StreamWriter(outFilename);
                    sw.AutoFlush = true;
                    var csv = new CsvWriter(sw);

                    csv.Configuration.RegisterClassMap<MyClassMap>();
                    csv.Configuration.Delimiter = "\t";
                    //csv.Configuration.AllowComments = true;

                    csv.WriteHeader<CacheEntry>();

                    csv.WriteRecords(appCompat.Cache.Entries);

                    sw.Close();
                }
                else
                {
                    //TODO do this
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
        public string SaveTo { get; set; }
    }

    public sealed class MyClassMap : CsvClassMap<CacheEntry>
    {
        public MyClassMap()
        {
            Map(m => m.Path);
            Map(m => m.LastModifiedTime);
        }
    }
    
}

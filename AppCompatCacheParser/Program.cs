using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AppCompatCache;
using Fclp;
using Microsoft.Win32;

namespace AppCompatCacheParser
{
    class Program
    {
        static void Main(string[] args)
        {

            //http://joshclose.github.io/CsvHelper/ ??

            //tsv
            //default is to dump live system to current working dir
            //live filename == WinXX_MachineName_AppCompatCache.tsv
            //hive filename == WinXX_HiveName_AppCompatCache.tsv

            //TODO Nlog color console

            var p = new FluentCommandLineParser<ApplicationArguments>();

            p.Setup(arg => arg.HiveFile).As('h',"HiveFile").WithDescription("Full path to hive file to process. If this option is not specified, the live Registry will be processed").SetDefault(string.Empty);
            p.Setup(arg => arg.SaveTo).As('s',"SaveTo").WithDescription("Directory to save results to (REQUIRED)").Required();
            p.Setup(arg => arg.FindEvidence).As('f',"FindEvidence").WithDescription("Be careful what you ask for").SetDefault(false);

            var header =
                $"AppCompatCache Parser version {Assembly.GetExecutingAssembly().GetName().Version}\r\nAuthor: Eric Zimmerman (saericzimmerman@gmail.com)";
            p.SetupHelp("?", "help").WithHeader(header).Callback(text => Console.WriteLine(text));
      

            var result =  p.Parse(args);

            if (result.HasErrors)
            {
                p.HelpOption.ShowHelp(p.Options);
                return;
            }

            if (p.Object.FindEvidence)
            {
                Console.WriteLine("\r\nThis is not the forensics program you are looking for...");
                return;
            }

            var hiveToProcess = "Live Registry";

            if (p.Object.HiveFile?.Length > 0)
            {
                hiveToProcess = p.Object.HiveFile;
            }

            Console.WriteLine(header);
            Console.WriteLine();

            Console.WriteLine($"Processing hive '{hiveToProcess}'");

            var outFileBase = "Something.tsv";
            var outFilename = Path.Combine(p.Object.SaveTo, outFileBase);

            Console.WriteLine($"Saving results to '{outFilename}'");

            Console.WriteLine();

            var appCompat = new AppCompatCache.AppCompatCache(p.Object.HiveFile);


            if ((appCompat.Cache != null))
            {
                Console.WriteLine($"Found {appCompat.Cache.Entries.Count:N0} cache entries!");
                
                foreach (var cacheEntry in appCompat.Cache.Entries)
                {
                 //do stuff here
                }

            }

#if DEBUG
            Console.WriteLine("Press a key to exit");
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
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PseudoETWToNeo4jImport
{
    class Program
    {
        static void Main(string[] args)
        {
            // set settings
            Global.Settings.General.NumberOfReadThreads = 1;
            Global.Settings.General.OutputFolder = @"E:/thesis-data/pseudonymized/regression-import/";

            Global.Settings.Pseudonymizer.PseudonymizationSalt = "gaemtool";
            Global.Settings.Pseudonymizer.PathPrefix = @"d:/d/gaem/";

            Global.Settings.Doxygen.Process = true;
            Global.Settings.Doxygen.RootFolder = @"D:/thesis-data/doxygen/";
            Global.Settings.Doxygen.Pseudonymize = true;

            Global.Settings.Logs.Process = true;
            Global.Settings.Logs.RootFolder = @"D:/thesis-data/pseudonymized_regression/";

            // start stuff
            DateTime startTime = DateTime.Now;
            Console.WriteLine(startTime.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Data transformation started.");

            if (Global.Settings.Doxygen.Process)
            {
                Console.WriteLine(startTime.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Transforming doxygen.");
                new DoxygenDataTransformer().Transform();
                Console.WriteLine(startTime.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Finished transforming doxygen.");
            }

            if (Global.Settings.Logs.Process)
            {
                Console.WriteLine(startTime.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Transforming logs.");
                new EventDataTransformer().Transform();
                Console.WriteLine(startTime.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Finished transforming logs.");
            }

            DateTime stopTime = DateTime.Now;
            Console.WriteLine(stopTime.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Data transformation finished.");

            Console.WriteLine(DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.ffff") + " | INFO | Total processing time: " + (stopTime - startTime).ToString());

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Console.WriteLine("Please do it again.");
            Console.ReadKey();
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PseudoETWToNeo4jImport
{
    public static class Global
    {
        public static long IdCounter = 0;
        public static ConcurrentDictionary<string, long> FunctionIds = new ConcurrentDictionary<string, long>();

        public static class Settings
        {
            public static class General
            {
                public static int NumberOfReadThreads { get; set; }
                public static string OutputFolder { get; set; }
            }

            public static class Pseudonymizer
            {
                public static string PseudonymizationSalt { get; set; }
                public static string PathPrefix { get; set; }
            }

            public static class Doxygen
            {
                public static bool Process { get; set; }
                public static string RootFolder { get; set; }
                public static bool Pseudonymize { get; set; }
            }

            public static class Logs
            {
                public static bool Process { get; set; }
                public static string RootFolder { get; set; }
            }
        }
    }
}

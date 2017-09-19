using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PseudoETWToNeo4jImport
{
    public abstract class DataTransformer
    {
        public abstract void Transform();

        protected BlockingCollection<string> CreateBlockingETLCollection(string path, string searchpattern)
        {
            var allFiles = Directory.GetFiles(path, searchpattern, SearchOption.AllDirectories);
            var filePaths = new BlockingCollection<string>(allFiles.Count());
            foreach (var fileName in allFiles)
            {
                filePaths.Add(fileName);
            }
            filePaths.CompleteAdding();
            return filePaths;
        }

        protected void WriteToFile(string fileName, BlockingCollection<string> collectionToWatch, int MAX_BUFFER)
        {
            Directory.CreateDirectory(Global.Settings.General.OutputFolder);

            using (FileStream fs = new FileStream(Global.Settings.General.OutputFolder + fileName, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(fs, Encoding.UTF8, MAX_BUFFER))
                {
                    writer.AutoFlush = true;

                    string toWrite;
                    while (!collectionToWatch.IsCompleted)
                    {
                        if (!collectionToWatch.TryTake(out toWrite)) {
                            Task.Delay(100);
                            continue;
                        }

                        //writer.WriteLine(collectionToWatch.Take());

                        writer.WriteLine(toWrite);
                    }

                    writer.Flush();
                }
            }
        }
    }
}

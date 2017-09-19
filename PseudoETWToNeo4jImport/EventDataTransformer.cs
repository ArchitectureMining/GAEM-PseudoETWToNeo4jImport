using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PseudoETWToNeo4jImport
{
    public class EventDataTransformer : DataTransformer
    {
        // streamwriter buffer in bytes
        int MAX_BUFFER = 1024 * 1024 * 4;

        BlockingCollection<string> startEventStrings = new BlockingCollection<string>();
        BlockingCollection<string> stopEventStrings = new BlockingCollection<string>();
        BlockingCollection<string> callStrings = new BlockingCollection<string>();

        BlockingCollection<string> startsRelationStrings = new BlockingCollection<string>();
        BlockingCollection<string> stopsRelationStrings = new BlockingCollection<string>();
        BlockingCollection<string> instanceofRelationStrings = new BlockingCollection<string>();
        BlockingCollection<string> invokesRelationStrings = new BlockingCollection<string>();

        public override void Transform()
        {
            BlockingCollection<string> fileCollection = CreateBlockingETLCollection(Global.Settings.Logs.RootFolder, "*.csv");
            int totalAmountOfFiles = fileCollection.Count();
            int taskCount;

            if (totalAmountOfFiles < Environment.ProcessorCount)
            {
                taskCount = totalAmountOfFiles;
            }
            else
            {
                //taskCount = Environment.ProcessorCount;
                taskCount = Global.Settings.General.NumberOfReadThreads;
            }

            Task[] writeTaskArray = new Task[7];

            // startEvent
            //writeTaskArray[0] = Task.Factory.StartNew(() => WriteToFile("startEventNodes.csv", startEventStrings, MAX_BUFFER));
            writeTaskArray[0] = Task.Factory.StartNew(() => WriteToFile("startEventNodes.csv", startEventStrings, MAX_BUFFER));
            // stopEvent
            //writeTaskArray[1] = Task.Factory.StartNew(() => WriteToFile("stopEventNodes.csv", stopEventStrings, MAX_BUFFER));
            writeTaskArray[1] = Task.Factory.StartNew(() => WriteToFile("stopEventNodes.csv", stopEventStrings, MAX_BUFFER));
            // calls
            //writeTaskArray[2] = Task.Factory.StartNew(() => WriteToFile("callNodes.csv", callStrings, MAX_BUFFER));
            writeTaskArray[2] = Task.Factory.StartNew(() => WriteToFile("callNodes.csv", callStrings, MAX_BUFFER));
            // starts relations
            //writeTaskArray[3] = Task.Factory.StartNew(() => WriteToFile("startsRelations.csv", startsRelationStrings, MAX_BUFFER));
            writeTaskArray[3] = Task.Factory.StartNew(() => WriteToFile("startsRelations.csv", startsRelationStrings, MAX_BUFFER));
            // stops relations
            //writeTaskArray[4] = Task.Factory.StartNew(() => WriteToFile("stopsRelations.csv", stopsRelationStrings, MAX_BUFFER));
            writeTaskArray[4] = Task.Factory.StartNew(() => WriteToFile("stopsRelations.csv", stopsRelationStrings, MAX_BUFFER));
            // instanceof relations
            //writeTaskArray[5] = Task.Factory.StartNew(() => WriteToFile("instanceofRelations.csv", instanceofRelationStrings, MAX_BUFFER));
            writeTaskArray[5] = Task.Factory.StartNew(() => WriteToFile("instanceofRelations.csv", instanceofRelationStrings, MAX_BUFFER));
            // invokes relations
            //writeTaskArray[6] = Task.Factory.StartNew(() => WriteToFile("invokesRelations.csv", invokesRelationStrings, MAX_BUFFER));
            writeTaskArray[6] = Task.Factory.StartNew(() =>WriteToFile("invokesRelations.csv", invokesRelationStrings, MAX_BUFFER));

            int filesHandled = 0;
            Task[] transformTaskkArray = new Task[taskCount];

            for (int i = 0; i < taskCount; i++)
            {
                transformTaskkArray[i] = Task.Factory.StartNew(() =>
                {
                    string fileName;

                    while (!fileCollection.IsCompleted)
                    {
                        if (!fileCollection.TryTake(out fileName)) continue;

                        HandleFile(fileName);
                        Interlocked.Increment(ref filesHandled);
                        Console.WriteLine(DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss.ff") + " | INFO | Files completed: " + filesHandled + " of " + totalAmountOfFiles);
                    }
                });
            }

            Task.WaitAll(transformTaskkArray);
            startEventStrings.CompleteAdding();
            stopEventStrings.CompleteAdding();
            callStrings.CompleteAdding();
            startsRelationStrings.CompleteAdding();
            stopsRelationStrings.CompleteAdding();
            instanceofRelationStrings.CompleteAdding();
            invokesRelationStrings.CompleteAdding();
            Task.WaitAll(writeTaskArray);
        }

        private void HandleFile(string fileName)
        {
            // event format
            // :LABEL,timestamp,run,eventorder,run_eventorder:ID
            // id:ID,timestamp,run,eventorder

            // call format
            // :LABEL,fullname,run,callorder,run_callorder:ID
            // id:ID,fullname,run,callorder

            // relation format
            // :TYPE,:START_ID,:END_ID
            // :START_ID,:END_ID

            Stack<OpenFunction> openFunctions = new Stack<OpenFunction>(1000000);
            string runName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", string.Empty);

            using (StreamReader reader = new StreamReader(fileName))
            {
                long eventOrder = 0;
                long callOrder = 0;

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    eventOrder++;
                    string eventId = Interlocked.Increment(ref Global.IdCounter).ToString();

                    string[] data = line.Split(',');
                    data[2] = data[2].Replace("\\", "/");

                    switch (data[0])
                    {
                        case "FunctionBegin":
                            {
                                callOrder++;
                                string callId = Interlocked.Increment(ref Global.IdCounter).ToString();

                                if (openFunctions.Count > 0)
                                {
                                    // create startEvent
                                    startEventStrings.Add(string.Join(",", new[] { eventId, data[1], runName, eventOrder.ToString() }));
                                    // create call
                                    callStrings.Add(string.Join(",", new[] { callId, data[3], runName, callOrder.ToString() }));
                                    // create startEvent-[STARTS]->call
                                    startsRelationStrings.Add(string.Join(",", new[] { eventId, callId }));
                                    // create call-[INSTANCEOF]->function
                                    if (Global.FunctionIds.ContainsKey(data[3]))
                                        instanceofRelationStrings.Add(string.Join(",", new[] { callId, Global.FunctionIds[data[3]].ToString() }));
                                    // create call-[INVOKES]->call
                                    invokesRelationStrings.Add(string.Join(",", new[] { openFunctions.Peek().Id , callId }));
                                }
                                else
                                {
                                    // create startEvent
                                    startEventStrings.Add(string.Join(",", new[] { eventId, data[1], runName, eventOrder.ToString() }));
                                    // create call
                                    callStrings.Add(string.Join(",", new[] { callId, data[3], runName, callOrder.ToString() }));
                                    // create startEvent-[STARTS]->call
                                    startsRelationStrings.Add(string.Join(",", new[] { eventId, callId }));
                                    // create call-[INSTANCEOF]->function
                                    if (Global.FunctionIds.ContainsKey(data[3]))
                                        instanceofRelationStrings.Add(string.Join(",", new[] { callId, Global.FunctionIds[data[3]].ToString() }));
                                }

                                openFunctions.Push(new OpenFunction() { FunctionName = data[3], Id = callId });
                            }
                            break;
                        case "FunctionEnd":
                            {
                                if (openFunctions.Count == 0)
                                {
                                    // create stopEvent
                                    stopEventStrings.Add(string.Join(",", new[] { eventId, data[1], runName, eventOrder.ToString() }));
                                }
                                else if (openFunctions.Peek().FunctionName == data[3])
                                {
                                    OpenFunction openFunction = openFunctions.Pop();
                                    
                                    // create stopEvent
                                    stopEventStrings.Add(string.Join(",", new[] { eventId, data[1], runName, eventOrder.ToString() }));
                                    // create stopEvent-[STOPS]->call
                                    stopsRelationStrings.Add(string.Join(",", new[] { eventId, openFunction.Id }));
                                }
                                else
                                {
                                    // create stopEvent
                                    stopEventStrings.Add(string.Join(",", new[] { eventId, data[1], runName, eventOrder.ToString() }));
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException("ERROR: Event type '" + data[0] + "' is not implemented.");
                    }
                }
            }
        }       

        private struct OpenFunction
        {
            public string FunctionName { get; set; }
            public string Id { get; set; }
        }
    }
}

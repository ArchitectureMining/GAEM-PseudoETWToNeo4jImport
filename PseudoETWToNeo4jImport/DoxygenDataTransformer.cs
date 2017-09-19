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
using System.Xml.Linq;

using PseudoETWToNeo4jImport.Pseudonymizers;

namespace PseudoETWToNeo4jImport
{
    public class DoxygenDataTransformer : DataTransformer
    {
        // streamwriter buffer in bytes
        int MAX_BUFFER = 1024 * 1024 * 4;

        ConcurrentDictionary<string, DoxyClassStruct> classNodes = new ConcurrentDictionary<string, DoxyClassStruct>();
        ConcurrentDictionary<string, DoxyDir> dirNodes = new ConcurrentDictionary<string, DoxyDir>();
        ConcurrentDictionary<string, DoxyFile> fileNodes = new ConcurrentDictionary<string, DoxyFile>();
        ConcurrentDictionary<string, DoxyFunction> functionNodes = new ConcurrentDictionary<string, DoxyFunction>();
        ConcurrentDictionary<string, DoxyNamespace> namespaceNodes = new ConcurrentDictionary<string, DoxyNamespace>();
        ConcurrentDictionary<string, DoxyStruct> structNodes = new ConcurrentDictionary<string, DoxyStruct>();

        BlockingCollection<string> inRelations = new BlockingCollection<string>();

        public override void Transform()
        {
            BlockingCollection<string> fileCollection = CreateBlockingETLCollection(Global.Settings.Doxygen.RootFolder, "*.xml");
            BlockingCollection<string> fileCollection2 = CreateBlockingETLCollection(Global.Settings.Doxygen.RootFolder, "*.xml");
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
                    }
                });
            }
            Task.WaitAll(transformTaskkArray);

            Task[] transformTaskArray2 = new Task[taskCount];
            for (int i = 0; i < taskCount; i++)
            {
                transformTaskArray2[i] = Task.Factory.StartNew(() =>
                {
                    string fileName;

                    while (!fileCollection2.IsCompleted)
                    {
                        if (!fileCollection2.TryTake(out fileName)) continue;

                        HandleFile2(fileName);
                    }
                });
            }
            Task.WaitAll(transformTaskArray2);

            inRelations.CompleteAdding();

            // classStructNodes
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "classNodes.csv"))
            {
                foreach (var value in classNodes.Values)
                {
                    writer.WriteLine(value.ToNeo4jImportLine());
                }
                classNodes.Clear();
            }

            // dirNodes
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "dirNodes.csv"))
            {
                foreach (var value in dirNodes.Values)
                {
                    writer.WriteLine(value.ToNeo4jImportLine());
                }
                dirNodes.Clear();
            }

            // fileNodes
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "fileNodes.csv"))
            {
                foreach (var value in fileNodes.Values)
                {
                    writer.WriteLine(value.ToNeo4jImportLine());
                }
                fileNodes.Clear();
            }

            // functionNodes
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "functionNodes.csv"))
            {
                foreach (var keyValuePair in functionNodes)
                {
                    writer.WriteLine(keyValuePair.Value.ToNeo4jImportLine());
                    Global.FunctionIds[keyValuePair.Key] = keyValuePair.Value.Id;
                }
                functionNodes.Clear();
            }

            // namespaceNodes
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "namespaceNodes.csv"))
            {
                foreach (var value in namespaceNodes.Values)
                {
                    writer.WriteLine(value.ToNeo4jImportLine());
                }
                namespaceNodes.Clear();
            }

            // namespaceNodes
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "structNodes.csv"))
            {
                foreach (var value in structNodes.Values)
                {
                    writer.WriteLine(value.ToNeo4jImportLine());
                }
                structNodes.Clear();
            }

            // inRelations
            using (StreamWriter writer = new StreamWriter(Global.Settings.General.OutputFolder + "inRelations.csv"))
            {
                while(!inRelations.IsCompleted)
                {
                    writer.WriteLine(inRelations.Take());
                }
            }
        }

        private void HandleFile(string filePath)
        {
            Pseudonymizer Pseudonymizer = Global.Settings.Doxygen.Pseudonymize ? new Pseudonymizer() : new DummyPseudonymizer();

            if (Path.GetFileName(filePath) == "index.xml")
            {
                // log that it's ignored
                return;
            }

            // else
            using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
            {
                XDocument doc = XDocument.Load(reader);
                XElement compounddef = doc.Root.Element("compounddef");

                string id = compounddef.Attribute("id").Value;
                string kind = compounddef.Attribute("kind").Value;
                string compoundname = compounddef.Element("compoundname").Value;


                switch (kind)
                {
                    case "class":
                        {
                            compoundname = compoundname.Replace("::", ".");
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeHierarchy(compoundname);

                            if (!classNodes.ContainsKey(pseudoCompoundName))
                            {
                                // create class
                                classNodes[pseudoCompoundName] = new DoxyClass(
                                        Interlocked.Increment(ref Global.IdCounter),
                                        compoundname.Split(new[] { '.' }, StringSplitOptions.None).Last(),
                                        compoundname,
                                        compounddef.Attribute("language").Value,
                                        compounddef.Element("location").Attribute("file")?.Value,
                                        compounddef.Element("location").Attribute("bodyfile")?.Value
                                        );
                            }
                            else
                            {
                                if (compounddef.Element("location").Attribute("file")?.Value != null)
                                    classNodes[pseudoCompoundName].AddDeclarationFile(compounddef.Element("location").Attribute("file").Value);
                                if (compounddef.Element("location").Attribute("bodyfile")?.Value != null)
                                    classNodes[pseudoCompoundName].AddBodyFile(compounddef.Element("location").Attribute("bodyfile").Value);
                            }

                            // create function
                            compounddef
                                .Elements("sectiondef")
                                    .Elements("memberdef")
                                    .Where(x => x.Attribute("kind").Value == "function")
                                    .Select(x => new DoxyFunction(
                                        Interlocked.Increment(ref Global.IdCounter), 
                                        x.Element("name").Value,
                                        compoundname + "." + x.Element("name").Value,
                                        x.Element("location")?.Attribute("file")?.Value,
                                        x.Element("location")?.Attribute("bodyfile")?.Value))
                                    .ToList()
                                    .ForEach(x => {
                                        if (!functionNodes.ContainsKey(x.Fullname))
                                        {
                                            functionNodes[x.Fullname] = x;
                                        }
                                        else
                                        {
                                            if (x.BodyFiles.Count > 0) functionNodes[x.Fullname].AddBodyFile(x.BodyFiles.First());
                                            if (x.DeclarationFiles.Count > 0) functionNodes[x.Fullname].AddDeclarationFile(x.DeclarationFiles.First());
                                        }
                                            // function in class
                                            inRelations.Add(string.Join(",", new[] {
                                                functionNodes[x.Fullname].Id.ToString(),
                                                classNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    });
                        }
                        break;
                    case "dir":
                        {
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeDirectoryPath(compoundname);
                            // create dir
                            dirNodes[pseudoCompoundName] = new DoxyDir(
                                Interlocked.Increment(ref Global.IdCounter), 
                                compoundname.Split(new[] { '/', '\\' }).Last(),
                                compoundname
                                );
                        }
                        break;
                    case "example":
                        // ignore
                        break;
                    case "group":
                        //ignore
                        break;
                    case "file":
                        {
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeFilePath(compounddef.Element("location").Attribute("file").Value);
                            // create file
                            fileNodes[pseudoCompoundName] = new DoxyFile(
                                Interlocked.Increment(ref Global.IdCounter),
                                compounddef.Element("location").Attribute("file").Value.Split(new[] { '/', '\\' }).Last(),
                                compounddef.Element("location").Attribute("file").Value
                                );
                        }
                        break;
                    case "interface":
                        // ignore
                        break;
                    case "namespace":
                        {
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeHierarchy(compoundname);
                            // create namespace
                            namespaceNodes[pseudoCompoundName] = new DoxyNamespace(
                                Interlocked.Increment(ref Global.IdCounter), 
                                compoundname.Split(new[] { '/', '\\' }).Last(),
                                compoundname,
                                compounddef.Attribute("language").Value
                                );
                        }
                        break;
                    case "page":
                        // ignore
                        break;
                    case "struct":
                        {
                            compoundname = compoundname.Replace("::", ".");
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeHierarchy(compoundname);

                            if (!structNodes.ContainsKey(pseudoCompoundName))
                            {
                                // create struct
                                structNodes[pseudoCompoundName] = new DoxyStruct(
                                    Interlocked.Increment(ref Global.IdCounter), 
                                    compoundname.Split(new[] { '.' }, StringSplitOptions.None).Last(),
                                    compoundname,
                                    compounddef.Attribute("language").Value,
                                    compounddef.Element("location").Attribute("file")?.Value,
                                    compounddef.Element("location").Attribute("bodyfile")?.Value
                                    );
                            }
                            else
                            {
                                if (compounddef.Element("location").Attribute("file")?.Value != null)
                                    structNodes[pseudoCompoundName].AddDeclarationFile(compounddef.Element("location").Attribute("file").Value);
                                if (compounddef.Element("location").Attribute("bodyfile")?.Value != null)
                                    structNodes[pseudoCompoundName].AddBodyFile(compounddef.Element("location").Attribute("bodyfile").Value);
                            }

                            // create function
                            compounddef
                                .Elements("sectiondef")
                                    .Elements("memberdef")
                                    .Where(x => x.Attribute("kind").Value == "function")
                                    .Select(x => new DoxyFunction(
                                        Interlocked.Increment(ref Global.IdCounter), 
                                        x.Element("name").Value,
                                        compoundname + "." + x.Element("name"),
                                        x.Element("location")?.Attribute("file")?.Value,
                                        x.Element("location")?.Attribute("bodyfile")?.Value))
                                    .ToList()
                                    .ForEach(x => {
                                        if (!functionNodes.ContainsKey(x.Fullname))
                                        {
                                            functionNodes[x.Fullname] = x;
                                        }
                                        else
                                        {
                                            if (x.BodyFiles.Count > 0) functionNodes[x.Fullname].AddBodyFile(x.BodyFiles.First());
                                            if (x.DeclarationFiles.Count > 0) functionNodes[x.Fullname].AddDeclarationFile(x.DeclarationFiles.First());
                                        }
                                            // function in class
                                            inRelations.Add(string.Join(",", new[] {
                                                functionNodes[x.Fullname].Id.ToString(),
                                                structNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    });
                        }
                        break;
                    case "union":
                        // ignore
                        break;
                    default:
                        throw new NotImplementedException("ERROR | Not implemented kind = " + kind);
                }
            }
        }

        private void HandleFile2(string filePath)
        {
            Pseudonymizer Pseudonymizer = Global.Settings.Doxygen.Pseudonymize ? new Pseudonymizer() : new DummyPseudonymizer();

            if (Path.GetFileName(filePath) == "index.xml")
            {
                // log that it's ignored
                return;
            }

            // else
            using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
            {
                XDocument doc = XDocument.Load(reader);
                XElement compounddef = doc.Root.Element("compounddef");

                string id = compounddef.Attribute("id").Value;
                string kind = compounddef.Attribute("kind").Value;
                string compoundname = compounddef.Element("compoundname").Value;
                
                switch (kind)
                {
                    case "class":
                        {
                            compoundname = compoundname.Replace("::", ".");
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeHierarchy(compoundname);

                            // struct in class
                            compounddef
                                .Elements("innerclass")
                                .Select(x => Pseudonymizer.PseudonymizeHierarchy(x.Value))
                                .ToList()
                                .ForEach(x => 
                                {
                                    if (structNodes.ContainsKey(x) && classNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            structNodes[x].Id.ToString(),
                                            classNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                });
                        }
                        break;
                    case "dir":
                        {
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeDirectoryPath(compoundname);
                            // dir in dir
                            compounddef
                                .Elements("innerdir")
                                .Select(x => Pseudonymizer.PseudonymizeDirectoryPath(x.Value))
                                .ToList()
                                .ForEach(x =>
                                {
                                    if (dirNodes.ContainsKey(x) && dirNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            dirNodes[x].Id.ToString(),
                                            dirNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                });

                            // file in dir
                            compounddef
                                .Elements("innerfile")
                                .Select(x => Pseudonymizer.PseudonymizeFilePath(compounddef.Element("location").Attribute("file").Value + x.Value))
                                .ToList()
                                .ForEach(x =>
                                {
                                    if (fileNodes.ContainsKey(x) && dirNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            fileNodes[x].Id.ToString(),
                                            dirNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                });
                        }
                        break;
                    case "example":
                        // ignore
                        break;
                    case "group":
                        //ignore
                        break;
                    case "file":
                        {
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeFilePath(compounddef.Element("location").Attribute("file").Value);
                            // struct/class in file
                            compounddef
                                .Elements("innerclass")
                                .Select(x => Pseudonymizer.PseudonymizeHierarchy(x.Value))
                                .ToList()
                                .ForEach(x => {
                                    if (classNodes.ContainsKey(x) && fileNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            classNodes[x].Id.ToString(),
                                            fileNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                    else if (structNodes.ContainsKey(x) && fileNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            structNodes[x].Id.ToString(),
                                            fileNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                });
                        }
                        break;
                    case "interface":
                        // ignore
                        break;
                    case "namespace":
                        {
                            string pseudoCompoundName = Pseudonymizer.PseudonymizeHierarchy(compoundname);
                            // class in namespace
                            compounddef
                                .Elements("innerclass")
                                .Select(x => Pseudonymizer.PseudonymizeHierarchy(x.Value))
                                .ToList()
                                .ForEach(x => {
                                    if (classNodes.ContainsKey(x) && namespaceNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            classNodes[x].Id.ToString(),
                                            namespaceNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                });

                            // namespace in namespace
                            compounddef
                                .Elements("innernamespace")
                                .Select(x => Pseudonymizer.PseudonymizeHierarchy(x.Value))
                                .ToList()
                                .ForEach(x => {
                                    if (namespaceNodes.ContainsKey(x) && namespaceNodes.ContainsKey(pseudoCompoundName))
                                    {
                                        inRelations.Add(string.Join(",", new[]
                                        {
                                            namespaceNodes[x].Id.ToString(),
                                            namespaceNodes[pseudoCompoundName].Id.ToString()
                                        }));
                                    }
                                });
                        }
                        break;
                    case "page":
                        // ignore
                        break;
                    case "struct":
                        // nothing 2nd time
                        break;
                    case "union":
                        // ignore
                        break;
                    default:
                        throw new NotImplementedException("ERROR | Not implemented kind = " + kind);
                }
            }
        }

        private abstract class DoxyElement
        {
            protected Pseudonymizer Pseudonymizer { get; } = Global.Settings.Doxygen.Pseudonymize ? new Pseudonymizer() : new DummyPseudonymizer();
            public long Id { get; set; }

            public abstract string ToNeo4jImportLine();
        }

        private abstract class DoxyClassStruct : DoxyElement
        {
            public string Kind { get; protected set; }
            public string Name { get; protected set; }
            public string Fullname { get; protected set; }
            public string Language { get; protected set; }
            public List<string> BodyFiles { get; protected set; } = new List<string>();
            public List<string> DeclarationFiles { get; protected set; } = new List<string>();

            public void AddBodyFile(string bodyfile)
            {
                string temp = Pseudonymizer.PseudonymizeFilePath(bodyfile);
                if (!BodyFiles.Contains(temp)) BodyFiles.Add(temp);
            }

            public void AddDeclarationFile(string declarationfile)
            {
                string temp = Pseudonymizer.PseudonymizeFilePath(declarationfile);
                if (!DeclarationFiles.Contains(temp)) DeclarationFiles.Add(temp);
            }

            public override abstract string ToNeo4jImportLine();
        }

        private class DoxyClass : DoxyClassStruct
        {
            // class format
            // :LABEL,name,fullname:ID,language,declarationfiles,bodyfiles
            // id:ID,name,fullname,language,declarationfiles,bodyfiles

            public DoxyClass(long id, string name, string fullname, string language, string declarationfile = null, string bodyfile = null)
            {
                Kind = "class";
                Id = id;
                Name = Pseudonymizer.PseudonymizeString(name);
                Fullname = Pseudonymizer.PseudonymizeHierarchy(fullname);
                Language = language;
                if (declarationfile != null)
                    DeclarationFiles.Add(Pseudonymizer.PseudonymizeFilePath(declarationfile));
                if (bodyfile != null)
                    BodyFiles.Add(Pseudonymizer.PseudonymizeFilePath(bodyfile));
                //if (Id == 34) Console.WriteLine(fullname);
            }

            public override string ToNeo4jImportLine()
            {
                return string.Join(",", new[] {
                    Id.ToString(),
                    Name,
                    Fullname,
                    Language,
                    string.Join(";", DeclarationFiles),
                    string.Join(";", BodyFiles)
                });
            }
        }

        private class DoxyDir : DoxyElement
        {
            // dir format
            // :LABEL,name,location:ID
            // id:ID,name,location

            public string Name { get; private set; }
            public string Location { get; private set; }

            public DoxyDir(long id, string name, string location)
            {
                Id = id;
                Name = Pseudonymizer.PseudonymizeString(name);
                Location = Pseudonymizer.PseudonymizeDirectoryPath(location);
                //if (Id == 36) Console.WriteLine(location);
            }

            public override string ToNeo4jImportLine()
            {
                return string.Join(",", new[] {
                    Id.ToString(),
                    Name,
                    Location
                });
            }
        }

        private class DoxyFile : DoxyElement
        {
            // file format
            // :LABEL,name,location:ID
            // id:ID,name,location

            public string Name { get; private set; }
            public string Location { get; protected set; }

            public DoxyFile(long id, string name, string location)
            {
                Id = id;
                Name = Pseudonymizer.PseudonymizeString(name);
                Location = Pseudonymizer.PseudonymizeFilePath(location);
                //if (Id == 36) Console.WriteLine(location);
            }

            public override string ToNeo4jImportLine()
            {
                return string.Join(",", new[] {
                    Id.ToString(),
                    Name,
                    Location
                });
            }
        }

        private class DoxyFunction : DoxyElement
        {
            // function format
            // :LABEL,name,fullname:ID,declarationfiles,bodyfiles
            // id:ID,name,fullname,declarationfiles,bodyfiles

            public string Name { get; private set; }
            public string Fullname { get; private set; }
            public List<string> BodyFiles { get; private set; } = new List<string>();
            public List<string> DeclarationFiles { get; private set; } = new List<string>();

            public DoxyFunction(long id, string name, string fullname, string declarationfile = null, string bodyfile = null)
            {
                Id = id;
                Name = Pseudonymizer.PseudonymizeString(name);
                Fullname = Pseudonymizer.PseudonymizeHierarchy(fullname);
                if (declarationfile != null)
                    DeclarationFiles.Add(Pseudonymizer.PseudonymizeFilePath(declarationfile));
                if (bodyfile != null)
                    BodyFiles.Add(Pseudonymizer.PseudonymizeFilePath(bodyfile));
                //if (Id == 36) Console.WriteLine(declarationfile);
            }

            public void AddBodyFile(string bodyfile)
            {
                string temp = Pseudonymizer.PseudonymizeFilePath(bodyfile);
                if (!BodyFiles.Contains(temp)) BodyFiles.Add(temp);
            }

            public void AddDeclarationFile(string declarationfile)
            {
                string temp = Pseudonymizer.PseudonymizeFilePath(declarationfile);
                if (!DeclarationFiles.Contains(temp)) DeclarationFiles.Add(temp);
            }

            public override string ToNeo4jImportLine()
            {
                return string.Join(",", new[] {
                    Id.ToString(),
                    Name,
                    Fullname,
                    string.Join(";", DeclarationFiles),
                    string.Join(";", BodyFiles)
                });
            }
        }

        private class DoxyNamespace : DoxyElement
        {
            // namespace format
            // :LABEL,name,fullname:ID,language
            // id:ID,name,fullname,language

            public string Name { get; private set; }
            public string Fullname { get; private set; }
            public string Language { get; private set; }

            public DoxyNamespace(long id, string name, string fullname, string language)
            {
                Id = id;
                Name = Pseudonymizer.PseudonymizeString(name);
                Fullname = Pseudonymizer.PseudonymizeHierarchy(fullname);
                Language = language;
                //if (Id == 36) Console.WriteLine(fullname);
            }

            public override string ToNeo4jImportLine()
            {
                return string.Join(",", new[] {
                    Id.ToString(),
                    Name,
                    Fullname,
                    Language
                });
            }
        }

        private class DoxyStruct : DoxyClassStruct
        {
            // struct format
            // :LABEL,name,fullname:ID,language,declarationfiles,bodyfiles
            // id:ID,name,fullname,language,declarationfiles,bodyfiles
            
            public DoxyStruct(long id, string name, string fullname, string language, string declarationfile = null, string bodyfile = null)
            {
                Kind = "struct";
                Id = id;
                Name = Pseudonymizer.PseudonymizeString(name);
                Fullname = Pseudonymizer.PseudonymizeHierarchy(fullname);
                Language = language;
                if (declarationfile != null)
                    DeclarationFiles.Add(Pseudonymizer.PseudonymizeFilePath(declarationfile));
                if (bodyfile != null)
                    BodyFiles.Add(Pseudonymizer.PseudonymizeFilePath(bodyfile));
                //if (Id == 36) Console.WriteLine(declarationfile);
            }

            public override string ToNeo4jImportLine()
            {
                return string.Join(",", new[] {
                    Id.ToString(),
                    Name,
                    Fullname,
                    Language,
                    string.Join(";", DeclarationFiles),
                    string.Join(";", BodyFiles)
                });
            }
        }
    }
}

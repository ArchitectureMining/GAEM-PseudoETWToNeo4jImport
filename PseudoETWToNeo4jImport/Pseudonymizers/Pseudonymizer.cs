using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PseudoETWToNeo4jImport.Pseudonymizers
{
    public class Pseudonymizer
    {
        private HashAlgorithm HashAlgorithm { get; } = SHA256.Create();

        public virtual string PseudonymizeString(string input)
        {
            string inputWithSalt = input + Global.Settings.Pseudonymizer.PseudonymizationSalt;

            byte[] hashedInput = HashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(inputWithSalt));

            StringBuilder sBuilder = new StringBuilder();
            for (int j = 0; j < hashedInput.Length; j++)
            {
                sBuilder.Append(hashedInput[j].ToString("x2"));
            }

            return sBuilder.ToString();
        }

        public string PseudonymizeFilePath(string filePath)
        {
            // remove system specific path
            filePath = filePath.Replace('\\', '/');
            filePath = filePath.Replace(Global.Settings.Pseudonymizer.PathPrefix, "");

            // hash all folder names (except last, that is the filename)
            string[] pathElements = filePath.Split(new [] { '/' }, StringSplitOptions.None); // original '\\' char
            for (int i = 0; i < pathElements.Length - 1; i++)
            {
                pathElements[i] = PseudonymizeString(pathElements[i]);
            }

            // handle filename
            int dotIndex = pathElements[pathElements.Length - 1].LastIndexOf('.');
            string hashedFileName = PseudonymizeString(pathElements[pathElements.Length - 1].Substring(0, dotIndex));

            pathElements[pathElements.Length - 1] = hashedFileName + pathElements[pathElements.Length - 1].Substring(dotIndex);

            return string.Join(@"/", pathElements); // original "\" slash
        }

        public string PseudonymizeDirectoryPath(string directoryPath)
        {
            directoryPath = directoryPath.Replace('\\', '/');
            directoryPath = directoryPath.Replace(Global.Settings.Pseudonymizer.PathPrefix, "");

            string[] pathElements = directoryPath.Split(new [] { '/' }, StringSplitOptions.None); // original '\\' char
            for (int i = 0; i < pathElements.Length - 1; i++)
            {
                pathElements[i] = PseudonymizeString(pathElements[i]);
            }

            return string.Join(@"/", pathElements); // original "\" slash
        }

        public string PseudonymizeFUNCSIG(string functionSignature)
        {
            // close < > brackets as those are not interesting
            string workInProgress = CloseGenericBrackets(functionSignature);
            // match the function with hierarchy out of the string
            workInProgress = Regex.Match(workInProgress, @"((?:[a-zA-Z0-9]+::)*(?:[a-zA-Z0-9]|operator .)+(?=\())").Value;
            // strip any possible whitespaces left
            workInProgress = Regex.Replace(workInProgress, " ", "");
            // return the split hierarchy
            string[] splitWorkInProgress = workInProgress.Split(new string[] { "::" }, StringSplitOptions.None);

            for (int i = 0; i < splitWorkInProgress.Length; i++)
            {
                splitWorkInProgress[i] = PseudonymizeString(splitWorkInProgress[i]);
            }

            return string.Join(".", splitWorkInProgress);
        }

        public string PseudonymizeHierarchy(string hierarchy)
        {
            string[] splitHierarchy = hierarchy.Split(new string[] { "::", "." }, StringSplitOptions.None);

            for (int i = 0; i < splitHierarchy.Length; i++)
            {
                splitHierarchy[i] = PseudonymizeString(splitHierarchy[i]);
            }

            return string.Join(".", splitHierarchy);
        }

        protected string CloseGenericBrackets(string message)
        {
            string output = message;

            int openBrackets = 0;
            int start = -1;
            int end = -1;

            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] == '<')
                {
                    if (openBrackets == 0)
                    {
                        start = i;
                    }
                    openBrackets++;
                    for (int j = i + 1; j < output.Length; j++)
                    {
                        if (output[j] == '<')
                        {
                            openBrackets++;
                        }
                        else if (output[j] == '>')
                        {
                            if (openBrackets == 1 || j == output.Length - 1)
                            {
                                end = j;
                                output = output.Remove(start, end - start + 1);
                            }
                            openBrackets--;
                            if (openBrackets == 0 || j == output.Length - 1)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return output;
        }
    }
}

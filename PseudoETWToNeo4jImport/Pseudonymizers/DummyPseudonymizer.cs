using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PseudoETWToNeo4jImport.Pseudonymizers
{
    public class DummyPseudonymizer : Pseudonymizer
    {
        public override string PseudonymizeString(string input)
        {
            return input;
        }
    }
}

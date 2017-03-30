using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SonarQube.Common
{
    /// <summary>
    /// Thrown when a there is an error that is well handled and should cause the process to exit in a clean way. 
    /// The message will be logged and the process will return exit code 1.
    /// </summary>
    public class AnalysisException : Exception
    {
        public AnalysisException(string message) : base(message)
        {
        }
    }
}

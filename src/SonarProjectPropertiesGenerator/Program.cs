//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sonar.Common;

namespace SonarProjectPropertiesGenerator
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length != 4)
            {
                Console.WriteLine("Expected to be called with exactly 4 arguments:");
                Console.WriteLine("  1) SonarQube Project Key");
                Console.WriteLine("  2) SonarQube Project Name");
                Console.WriteLine("  3) SonarQube Project Version");
                Console.WriteLine("  4) Dump folder path");
                return 1;
            }

            var dumpFolderPath = args[3];

            var projects = ProjectLoader.LoadFrom(dumpFolderPath);
            var contents = PropertiesWriter.ToString(new ConsoleLogger(includeTimestamp: true), args[0], args[1], args[2], dumpFolderPath, projects);

            File.WriteAllText(Path.Combine(dumpFolderPath, "sonar-project.properties"), contents, Encoding.ASCII);

            return 0;
        }
    }
}

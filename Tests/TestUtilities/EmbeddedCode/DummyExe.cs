//-----------------------------------------------------------------------
// <copyright file="DummyExe.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

/*
    This file contains the template for an executable that writes the supplied
    arguments to a log file and returns a specified exit code.
    The template is embedded as a resource; it is not compiled as part of the
    test assembly.

    To use the template, load the resource as a string and replace the
    string "EXITCODE_PLACEHOLDER" with the exit code value to be returned.

    To embed additional code into the executable (e.g. to throw an exception
    or to add a delay), set the string "ADDITIONALCODE_PLACEHOLDER" to the
    code to be included.
*/

using System.IO;

namespace SonarQube.Bootstrapper.Tests.Dummy
{
    class Program
    {
        static int Main(string[] args)
        {
            string logFile = Path.ChangeExtension(Path.Combine(typeof(Program).Assembly.Location), "log");

            File.WriteAllLines(logFile, args);

            int exitCode = EXITCODE_PLACEHOLDER;

            ADDITIONALCODE_PLACEHOLDER

            return exitCode;
        }
    }
}

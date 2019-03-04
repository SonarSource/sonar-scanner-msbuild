/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2019 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

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

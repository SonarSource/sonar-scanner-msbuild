//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System;

namespace SonarQube.V0_9UpgradeMessageExe
{
    /* The file names of the bootstrapper, pre- and post- processor exes changed
     * between version 0.9 and 1.0 (a breaking change).
     * This exe exists to provide a slightly better user experience for one
     * scenario, namely where the user is running v0.9 of the bootstrapper but
     * has upgraded to a later version of the C# plug-in.
     *
     * The v0.9 bootstrapper will download the zip from the server and then attempt
     * to execute "SonarQube.MSBuild.PreProcessor.exe".
     *
     * This exe (also called "SonarQube.MSBuild.PreProcessor.exe") writes an error
     * message and exits with an error code that will cause the build to fail.
     */

    public static class Program
    {
        static int Main()
        {
            Console.Error.WriteLine(Resources.UpgradeMessage);
            return 1;
        }
    }
}

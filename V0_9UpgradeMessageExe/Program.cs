/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
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

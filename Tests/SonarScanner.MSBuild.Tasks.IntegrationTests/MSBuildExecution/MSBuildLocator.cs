/*
 * SonarScanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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
using System.IO;
using Microsoft.VisualStudio.Setup.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarScanner.MSBuild.Tasks.IntegrationTests
{
    /// <summary>
    /// Utility class that locates MSBuild executables
    /// </summary>
    internal static class MSBuildLocator
    {
        /// <summary>
        /// Returns the path to the specified version of msbuild.exe or
        /// null if it could not be found
        /// </summary>
        /// <param name="msBuildMajorVersion">MSBuild major version number e.g. 15.0</param>
        public static string GetMSBuildPath(string msBuildMajorVersion, TestContext testContext)
        {
            testContext.WriteLine($"Test setup: attempting to location MSBuild instance. Version: {msBuildMajorVersion}");
            ISetupConfiguration config = new SetupConfiguration();

            var instances = new ISetupInstance[100];
            var enumerator = config.EnumInstances();
            enumerator.Next(100, instances, out int fetched);

            if (fetched == 0)
            {
                throw new InvalidOperationException("Test setup error: no instances of Visual Studio could be located on this machine");
            }

            string partialExePath = Path.Combine("MSBuild", msBuildMajorVersion, "Bin", "msbuild.exe");

            for (int i = 0; i < fetched; i++)
            {
                var instance = instances[i];
                testContext.WriteLine($"\t\tVS instance: {instance.GetDisplayName()}, {instance.GetInstallationVersion()}, {instance.GetInstallationPath()}");

                var candidateExePath = instance.ResolvePath(partialExePath);

                if (File.Exists(candidateExePath))
                {
                    testContext.WriteLine($"\tMSBuild exe located: {candidateExePath}");
                    return candidateExePath;
                }
            }

            testContext.WriteLine($"Test setup: MSBuild exe could not be located");
            return null;
        }
    }
}

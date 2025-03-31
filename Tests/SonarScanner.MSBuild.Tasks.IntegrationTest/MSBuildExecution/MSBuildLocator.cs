/*
 * SonarScanner for .NET
 * Copyright (C) 2016-2025 SonarSource SA
 * mailto: info AT sonarsource DOT com
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

using Microsoft.VisualStudio.Setup.Configuration;

namespace SonarScanner.MSBuild.Tasks.IntegrationTest;

/// <summary>
/// Utility class that locates MSBuild executables
/// </summary>
internal static class MSBuildLocator
{
    /// <summary>
    /// Returns a path to an instance of msbuild.exe or null if one could
    /// not be found.
    /// </summary>
    /// <remarks>If there are multiple instances of VS on the machine there is no guarantee which
    /// one will be returned, except that instances of VS2019 or later will be returned in preference
    /// to VS2017.</remarks>
    public static string GetMSBuildPath(TestContext testContext)
    {
        testContext.WriteLine($"Test setup: attempting to locate an MSBuild instance...");

        var path = GetMSBuildPath("Current", testContext) // VS2019 or later
            ?? GetMSBuildPath("15.0", testContext); // VS2017

        if (path == null)
        {
            testContext.WriteLine($"Test setup: failed to locate any version of MSBuild");
        }
        return path;
    }

    /// <summary>
    /// Returns the path to the specified version of msbuild.exe or
    /// null if it could not be found
    /// </summary>
    /// <param name="msBuildMajorVersion">MSBuild major version number e.g. 15.0</param>
    private static string GetMSBuildPath(string msBuildMajorVersion, TestContext testContext)
    {
        // Note: we're using a Microsoft component that locates instances of VS, and then
        // we're searching for an expected path under VS.
        // A more robust and flexible approach would be to use https://www.nuget.org/packages/vswhere/
        // which would allow us to search for a specific version of MSBuild directly.
        testContext.WriteLine($"Test setup: attempting to locate an MSBuild instance. Version: {msBuildMajorVersion}");
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

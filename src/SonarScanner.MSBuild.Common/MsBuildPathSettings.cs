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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SonarScanner.MSBuild.Common
{
    public class MsBuildPathSettings : IMsBuildPathsSettings
    {
        // Not supported versions of MSBuild are listed too, to allow us to throw
        // errors from the Integration targets in case we detect we are running under
        // not supported MSBuild.
        private readonly string[] msBuildVersions = new[] { "4.0", "10.0", "11.0", "12.0", "14.0", "15.0", };

        private readonly Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> environmentGetFolderPath;
        private readonly Func<bool> isWindows;
        private readonly Func<string, bool> fileExists;

        public MsBuildPathSettings() : this(Environment.GetFolderPath, PlatformHelper.IsWindows, File.Exists)
        {
        }

        public /* for testing purposes */ MsBuildPathSettings(
            Func<Environment.SpecialFolder, Environment.SpecialFolderOption, string> environmentGetFolderPath,
            Func<bool> isWindows,
            Func<string, bool> fileExists)
        {
            this.environmentGetFolderPath = environmentGetFolderPath;
            this.isWindows = isWindows;
            this.fileExists = fileExists;
        }

        public IEnumerable<string> GetImportBeforePaths()
        {
            var msBuildUserExtensionsPaths = GetLocalApplicationDataPaths()
                .Distinct()
                .SelectMany(appData => msBuildVersions.Select(msBuildVersion => GetMsBuildImportBeforePath(appData, msBuildVersion)))
                .ToList();

            if (msBuildUserExtensionsPaths.Count == 0)
            {
                throw new IOException("Cannot find local application data directory.");
            }

            msBuildUserExtensionsPaths.AddRange(DotnetImportBeforePathsLinuxMac());

            return msBuildUserExtensionsPaths;
        }

        private IEnumerable<string> DotnetImportBeforePathsLinuxMac()
        {
            if (this.isWindows())
            {
                return Enumerable.Empty<string>();
            }

            var userProfilePath = this.environmentGetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.Create);

            if (string.IsNullOrEmpty(userProfilePath))
            {
                throw new IOException("Cannot find user profile directory.");
            }

            // VAL: the comment below seems wrong. The path below does not work on Ubuntu 16.04 and
            // .NET Core SDK 2.1.500: I copied a target file that prints a high importance message and ran
            // "dotnet build"; no message in output. It might work on different OS or SDK, hence I am
            // keeping this method, but we need to do a more robust test in order to delete the code.
            // NOTE: the message is printed if the target file is copied in supported extension points.
            // The supported extension points are documented in the article below:
            // https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2017
            // MSBuildExtensionsPath --> in Program Files
            // MSBuildUserExtensionsPath --> in Local AppData

            // "dotnet build" and "dotnet msbuild" on non-Windows use a different path for import before
            return new[] { GetMsBuildImportBeforePath(userProfilePath, "15.0") }; // Older versions are not supported on non-Windows OS
        }

        /// <summary>
        /// Returns the local AppData path for the current user. This method will return multiple paths if running as Local System.
        /// </summary>
        private IEnumerable<string> GetLocalApplicationDataPaths()
        {
            var localAppData = this.environmentGetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

            // Return empty enumerable when Local AppData is empty. In this case an exception should be thrown at the call site.
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                yield break;
            }

            yield return localAppData;

            // The code below is Windows-specific, no need to be executed on non-Windows platforms.
            if (!isWindows())
            {
                yield break;
            }

            // When running under Local System account on a 64bit OS, the local application data folder
            // is inside %windir%\system32
            // When a process copies a file in this location, the OS will automatically redirect it to:
            // for 32bit processes - %windir%\sysWOW64\...
            // for 64bit processes - %windir%\system32\...
            // Nice explanation could be found here:
            // https://www.howtogeek.com/326509/whats-the-difference-between-the-system32-and-syswow64-folders-in-windows/
            // If a 32bit process needs to copy files to %windir%\system32, it should use %windir%\sysnative
            // to avoid the redirection:
            // https://docs.microsoft.com/en-us/windows/desktop/WinProg64/file-system-redirector
            // We need to copy the ImportBefore.targets in both locations to ensure that both the 32bit and 64bit versions
            // of MSBuild will be able to pick them up.
            var systemPath = environmentGetFolderPath(Environment.SpecialFolder.System, Environment.SpecialFolderOption.None); // %windir%\System32
            if (!string.IsNullOrWhiteSpace(systemPath) &&
                localAppData.StartsWith(systemPath)) // We are under %windir%\System32 => we are running as System Account
            {
                var systemX86Path = environmentGetFolderPath(Environment.SpecialFolder.SystemX86, Environment.SpecialFolderOption.None); // %windir%\SysWOW64 (or System32 on 32bit windows)
                var localAppDataX86 = localAppData.Replace(systemPath, systemX86Path);
                if (fileExists(localAppDataX86))
                {
                    yield return localAppDataX86;
                }

                var sysNativePath = Path.Combine(Path.GetDirectoryName(systemPath), "Sysnative"); // %windir%\Sysnative
                var localAppDataX64 = localAppData.Replace(systemPath, sysNativePath);
                if (fileExists(localAppDataX64))
                {
                    yield return localAppDataX64;
                }
            }
        }

        public IEnumerable<string> GetGlobalTargetsPaths()
        {
            var programFiles = this.environmentGetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.None);

            if (string.IsNullOrWhiteSpace(programFiles))
            {
                return Enumerable.Empty<string>();
            }

            return new[]
            {
                Path.Combine(programFiles, "MSBuild", "14.0", "Microsoft.Common.Targets", "ImportBefore"),
                Path.Combine(programFiles, "MSBuild", "15.0", "Microsoft.Common.Targets", "ImportBefore"),
            };
        }

        private static string GetMsBuildImportBeforePath(string basePath, string msBuildVersion) =>
            Path.Combine(basePath, "Microsoft", "MSBuild", msBuildVersion, "Microsoft.Common.targets", "ImportBefore");
    }
}

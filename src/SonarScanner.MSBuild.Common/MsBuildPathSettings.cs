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

namespace SonarScanner.MSBuild.Common
{
    public class MsBuildPathSettings : IMsBuildPathsSettings
    {
        private readonly Func<Environment.SpecialFolder, bool, string> getEnvironmentSpecialFolderPath;
        private readonly Func<bool> isWindows;

        public MsBuildPathSettings()
        {
            this.getEnvironmentSpecialFolderPath = (folder, forceCreate) =>
                Environment.GetFolderPath(folder,
                    forceCreate ? Environment.SpecialFolderOption.Create : Environment.SpecialFolderOption.None);
            this.isWindows = PlatformHelper.IsWindows;
        }

        public /* for testing purposes */ MsBuildPathSettings(
            Func<Environment.SpecialFolder, bool, string> getEnvironmentSpecialFolderPath, Func<bool> isWindows)
        {
            this.getEnvironmentSpecialFolderPath = getEnvironmentSpecialFolderPath;
            this.isWindows = isWindows;
        }

        public IEnumerable<string> GetImportBeforePaths()
        {
            var appDataPath = this.getEnvironmentSpecialFolderPath(Environment.SpecialFolder.LocalApplicationData, true);

            if (string.IsNullOrEmpty(appDataPath))
            {
                throw new IOException("Cannot find local application data directory.");
            }

            yield return GetMsBuildImportBeforePath(appDataPath, "4.0");
            yield return GetMsBuildImportBeforePath(appDataPath, "10.0");
            yield return GetMsBuildImportBeforePath(appDataPath, "11.0");
            yield return GetMsBuildImportBeforePath(appDataPath, "12.0");
            yield return GetMsBuildImportBeforePath(appDataPath, "14.0");
            yield return GetMsBuildImportBeforePath(appDataPath, "15.0");

            if (!this.isWindows())
            {
                var userProfilePath = this.getEnvironmentSpecialFolderPath(Environment.SpecialFolder.UserProfile, true);

                if (string.IsNullOrEmpty(userProfilePath))
                {
                    throw new IOException("Cannot find user profile directory.");
                }

                // "dotnet build" and "dotnet msbuild" on non-Windows use a different path for import before
                yield return GetMsBuildImportBeforePath(userProfilePath, "15.0");
            }
        }

        public IEnumerable<string> GetGlobalTargetsPaths()
        {
            var programFiles = this.getEnvironmentSpecialFolderPath(Environment.SpecialFolder.ProgramFiles, false);

            if (string.IsNullOrEmpty(programFiles))
            {
                throw new IOException("Cannot find programs directory.");
            }

            yield return Path.Combine(programFiles, "MSBuild", "14.0", "Microsoft.Common.Targets", "ImportBefore");
        }

        private static string GetMsBuildImportBeforePath(string basePath, string msBuildVersion) =>
            Path.Combine(basePath, "Microsoft", "MSBuild", msBuildVersion, "Microsoft.Common.targets", "ImportBefore");
    }
}

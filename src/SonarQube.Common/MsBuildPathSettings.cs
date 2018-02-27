/*
 * SonarQube Scanner for MSBuild
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

namespace SonarQube.Common
{
    public class MsBuildPathSettings : IMsBuildPathsSettings
    {
        public IEnumerable<string> GetImportBeforePaths()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            yield return GetMsBuildImportBeforePath(appData, "15.0");
            yield return GetMsBuildImportBeforePath(appData, "14.0");

            if (!PlatformHelper.IsWindows())
            {
                var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // "dotnet build" and "dotnet msbuild" on non-Windows use a different path for import before
                yield return GetMsBuildImportBeforePath(userProfilePath, "15.0");
            }
        }

        public IEnumerable<string> GetGlobalTargetsPaths()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            yield return Path.Combine(programFiles, "MSBuild", "14.0", "Microsoft.Common.Targets", "ImportBefore");
            yield return Path.Combine(programFiles, "MSBuild", "12.0", "Microsoft.Common.Targets", "ImportBefore");
        }

        private static string GetMsBuildImportBeforePath(string basePath, string msBuildVersion) =>
            Path.Combine(basePath, "Microsoft", "MSBuild", msBuildVersion, "Microsoft.Common.targets", "ImportBefore");
    }
}

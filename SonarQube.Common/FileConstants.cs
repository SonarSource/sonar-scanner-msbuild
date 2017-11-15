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
using System.Collections.Generic;
using System.IO;

namespace SonarQube.Common
{
    public static class FileConstants
    {
        /// <summary>
        /// Name of the per-project file that contain information used
        /// during analysis and when generating the sonar-scanner.properties file
        /// </summary>
        public const string ProjectInfoFileName = "ProjectInfo.xml";

        /// <summary>
        /// Name of the file containing analysis configuration settings
        /// </summary>
        public const string ConfigFileName = "SonarQubeAnalysisConfig.xml";

        /// <summary>
        /// Name of the import before target file
        /// </summary>
        public const string ImportBeforeTargetsName = "SonarQube.Integration.ImportBefore.targets";

        /// <summary>
        /// Name of the targets file that contains the integration pieces
        /// </summary>
        public const string IntegrationTargetsName = "SonarQube.Integration.targets";

        /// <summary>
        /// Path to the user specific ImportBefore folders
        /// </summary>
        public static IReadOnlyList<string> ImportBeforeDestinationDirectoryPaths
        {
            get
            {
                return PlatformHelper.IsWindows() ? GetWindowsImportBeforePaths() : GetNonWindowsImportBeforePaths();
            }
        }

        public static IReadOnlyList<string> GetWindowsImportBeforePaths()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new string[]
            {
                Path.Combine(appData, "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine(appData, "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore")
            };
        }

        public static IReadOnlyList<string> GetNonWindowsImportBeforePaths()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return new string[]
            {
                // for msbuild
                Path.Combine(appData, "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore"),
                Path.Combine(appData, "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),

                // for "dotnet build" and "dotnet msbuild"
                Path.Combine(userProfile, "Microsoft", "MSBuild", "15.0", "Microsoft.Common.targets", "ImportBefore")
            };
        }

    }
}

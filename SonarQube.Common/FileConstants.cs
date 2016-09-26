//-----------------------------------------------------------------------
// <copyright file="FileConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

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
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return new string[]
                {
                    Path.Combine(appData, "Microsoft", "MSBuild", "14.0", "Microsoft.Common.targets", "ImportBefore"),
                    Path.Combine(appData, "Microsoft", "MSBuild", "12.0", "Microsoft.Common.targets", "ImportBefore")
                };
            }
        }
    }
}

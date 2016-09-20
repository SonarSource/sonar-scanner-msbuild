//-----------------------------------------------------------------------
// <copyright file="FileConstants.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

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
    }
}

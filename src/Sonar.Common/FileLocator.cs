//-----------------------------------------------------------------------
// <copyright file="FileLocator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using System.Diagnostics;
using System.IO;

namespace Sonar.Common
{
    /// <summary>
    /// Provides methods to locate installed SonarQube files and directories
    /// </summary>
    public static class FileLocator
    {
        public const string SonarRunnerFileName = "sonar-runner.bat";

        #region Public methods

        /// <summary>
        /// Searches the path for the sonar-runner config file
        /// </summary>
        /// <returns>The full path to the file, or null if the file could not be located</returns>
        public static string FindDefaultSonarRunnerProperties()
        {
            string configPath = null;

            // Try to find the properties file relative to the runner executable.
            // We're expecting the sonar-runner installation to be installed in a known set of directories
            // i.e.
            // [root]\lib - contains the jar files
            // [root]\bin - contains the executable
            // [root]\conf - contains the properties file
            string exePath = FindDefaultSonarRunnerExecutable();
            if (exePath != null)
            {
                configPath = Path.GetDirectoryName(exePath);
                configPath = Path.Combine(Path.GetDirectoryName(exePath), @"..\conf\sonar-runner.properties");
                configPath = Path.GetFullPath(configPath);
                    
                if (!File.Exists(configPath))
                {
                    Debug.Fail("Found the sonar-runner executable, but failed to locate the properties file. Expected to be at " + configPath);
                    configPath = null;
                }
            }

            return configPath;
        }

        /// <summary>
        /// Searches the path for the sonar-runner executable
        /// </summary>
        /// <returns>The full path to the file, or null if the file could not be located</returns>
        public static string FindDefaultSonarRunnerExecutable()
        {
            return NativeMethods.FindOnPath(SonarRunnerFileName);
        }

        #endregion
    }
}

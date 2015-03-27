//-----------------------------------------------------------------------
// <copyright file="ArgumentProcessor.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.IO;

namespace SonarQube.TeamBuild.PreProcessor
{
    /// <summary>
    /// Process the command line arguments and reports any errors.
    /// Can also look at environment variables.
    /// </summary>
    internal static class ArgumentProcessor
    {       
        #region Public methods

        /// <summary>
        /// Attempts to process the supplied command line arguments and 
        /// reports any errors using the logger.
        /// Returns null unless all of the properties are valid.
        /// </summary>
        public static ProcessedArgs TryProcessArgs(string[] commandLineArgs, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            
            ProcessedArgs processed = null;

            if (commandLineArgs == null || commandLineArgs.Length < 3 || commandLineArgs.Length > 4)
            {
                logger.LogError(Resources.ERROR_InvalidCommandLineArgs);
                return null;
            }

            string projectKey = commandLineArgs[0];
            string projectName = commandLineArgs[1];
            string projectVersion = commandLineArgs[2];

            string propertiesPath = null;
            if (commandLineArgs.Length == 4)
            {
                propertiesPath = commandLineArgs[3];
            }

            propertiesPath = TryResolveRunnerPropertiesPath(propertiesPath, logger);
            if (!string.IsNullOrEmpty(propertiesPath))
            {
                processed = new ProcessedArgs(projectKey, projectName, projectVersion, propertiesPath);
            }

            return processed;
        }

        #endregion

        #region Private methods

        private static string TryResolveRunnerPropertiesPath(string propertiesPath, ILogger logger)
        {
            if (string.IsNullOrEmpty(propertiesPath))
            {
                logger.LogMessage(Resources.DIAG_LocatingRunnerProperties);
                propertiesPath = FileLocator.FindDefaultSonarRunnerProperties();

                if (string.IsNullOrWhiteSpace(propertiesPath))
                {
                    logger.LogError(Resources.ERROR_FailedToLocateRunnerProperties);
                }
            }
            else
            {
                if (!File.Exists(propertiesPath))
                {
                    logger.LogError(Resources.ERROR_InvalidRunnerPropertiesLocationSupplied, propertiesPath);
                    propertiesPath = null;
                }
            }

            return propertiesPath;
        }

        #endregion

    }
}

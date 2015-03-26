//-----------------------------------------------------------------------
// <copyright file="TeamBuildSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using Sonar.Common;
using System;
using System.Collections;
using System.IO;

namespace Sonar.TeamBuild.Integration
{
    /// <summary>
    /// Provides access to TeamBuild-specific settings and settings calculated
    /// from those settings
    /// </summary>
    public class TeamBuildSettings
    {
        internal const string ConfigFileName = "SonarAnalysisConfig.xml";

        internal static class TeamBuildEnvironmentVariables
        {
            public const string IsInTeamBuild = "TF_Build";
            public const string TfsCollectionUri = "TF_BUILD_COLLECTIONURI";
            public const string BuildUri = "TF_BUILD_BUILDURI";
            public const string BuildDirectory = "TF_BUILD_BUILDDIRECTORY";
            //        public const string BinariesDirectory = "TF_BUILD_BINARIESDIRECTORY";

            // Other available environment variables:
            //TF_BUILD_BUILDDEFINITIONNAME: SimpleBuild1
            //TF_BUILD_BUILDNUMBER: SimpleBuild1_20150310.4
            //TF_BUILD_BUILDREASON: Manual
            //TF_BUILD_DROPLOCATION: 
            //TF_BUILD_SOURCEGETVERSION: C14
            //TF_BUILD_SOURCESDIRECTORY: C:\Builds\1\Demos\SimpleBuild1\src
            //TF_BUILD_TESTRESULTSDIRECTORY: C:\Builds\1\Demos\SimpleBuild1\tst
        }

        #region Public methods

        /// <summary>
        /// Factory method to create and return a new set of team build settings
        /// calculated from environment variables.
        /// Returns null if all of the required environment variables are not present.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static TeamBuildSettings GetSettingsFromEnvironment(ILogger logger)
        {
            TeamBuildSettings settings = null;

            bool environmentIsValid = CheckRequiredEnvironmentVariablesExist(logger,
                TeamBuildEnvironmentVariables.TfsCollectionUri,
                TeamBuildEnvironmentVariables.BuildDirectory,
                TeamBuildEnvironmentVariables.BuildUri);

            if (environmentIsValid)
            {
                // TODO: validate environment variables
                settings = new TeamBuildSettings()
                {
                    BuildUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildUri),
                    TfsUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.TfsCollectionUri),
                    BuildDirectory = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildDirectory)
                };
            };

            return settings;
        }

        public static bool IsInTeamBuild
        {
            get
            {
                string value = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.IsInTeamBuild);
                
                bool result;
                if (value != null && bool.TryParse(value, out result))
                {
                    return result;
                }
                return false;
            }
        }

        public string TfsUri
        {
            get;
            private set;
        }

        public string BuildUri
        {
            get;
            private set;
        }

        public string BuildDirectory
        {
            get;
            private set;
        }

        #endregion

        #region Public calculated properties

        public string SonarConfigDir
        {
            get
            {
                return Path.Combine(this.BuildDirectory, "SonarTemp", "Config");
            }
        }

        public string SonarOutputDir
        {
            get
            {
                return Path.Combine(this.BuildDirectory, "SonarTemp", "Output");
            }
        }

        public string AnalysisConfigFilePath
        {
            get { return Path.Combine(this.SonarConfigDir, ConfigFileName); }
        }

        #endregion
        
        #region Private methods

        private static bool CheckRequiredEnvironmentVariablesExist(ILogger logger, params string[] required)
        {
            IDictionary allVars = Environment.GetEnvironmentVariables();

            bool allFound = true;
            foreach (string requiredVar in required)
            {
                string value = allVars[requiredVar] as string;
                if (value == null || string.IsNullOrEmpty(value))
                {
                    if (logger != null)
                    {
                        logger.LogError(Resources.MissingEnvironmentVariable, requiredVar);
                    }
                    allFound = false;
                }
            }
            return allFound;
        }

        #endregion
    }
}

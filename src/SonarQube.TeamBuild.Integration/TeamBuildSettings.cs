//-----------------------------------------------------------------------
// <copyright file="TeamBuildSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Collections;
using System.IO;

namespace SonarQube.TeamBuild.Integration
{
    /// <summary>
    /// Provides access to TeamBuild-specific settings and settings calculated
    /// from those settings
    /// </summary>
    public class TeamBuildSettings
    {
        internal const string ConfigFileName = "SonarQubeAnalysisConfig.xml";

        internal const int DefaultLegacyCodeCoverageTimeout = 30000; // ms

        internal static class EnvironmentVariables
        {
            /// <summary>
            /// Name of the environment variable that specifies the directory to use as the 
            /// analysis root in non-TFS cases
            /// </summary>
            public const string SQAnalysisRootPath = "SQ_BUILDDIRECTORY";

            /// <summary>
            /// Name of the environment variable that specifies whether the processing
            /// of code coverage reports in legacy TeamBuild cases should be skipped
            /// </summary>
            public const string SkipLegacyCodeCoverage = "SQ_SkipLegacyCodeCoverage";

            /// <summary>
            /// Name of the environment variable that specifies how long to spend
            /// attempting to retrieve code coverage reports in legacy TeamBuild cases
            /// </summary>
            public const string LegacyCodeCoverageTimeoutInMs = "SQ_LegacyCodeCoverageInMs";

            public const string IsInTeamBuild = "TF_Build"; // Common to legacy and non-legacy TeamBuilds

            // Legacy TeamBuild environment variables (XAML Builds)
            public const string TfsCollectionUri_Legacy = "TF_BUILD_COLLECTIONURI";
            public const string BuildUri_Legacy = "TF_BUILD_BUILDURI";
            public const string BuildDirectory_Legacy = "TF_BUILD_BUILDDIRECTORY";

            // TFS 2015 Environment variables
            public const string TfsCollectionUri_TFS2015 = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";
            public const string BuildUri_TFS2015 = "BUILD_BUILDURI";
            public const string BuildDirectory_TFS2015 = "AGENT_BUILDDIRECTORY";
        }

        #region Public static methods

        /// <summary>
        /// Factory method to create and return a new set of team build settings
        /// calculated from environment variables.
        /// Returns null if all of the required environment variables are not present.
        /// </summary>
        public static TeamBuildSettings GetSettingsFromEnvironment(ILogger logger)
        {
            TeamBuildSettings settings = null;

            BuildEnvironment env = GetBuildEnvironemnt();
            switch (env)
            {
                case BuildEnvironment.LegacyTeamBuild:

                    logger.LogMessage(Resources.SETTINGS_InLegacyTeamBuild);
                    settings = new TeamBuildSettings()
                    {
                        BuildEnvironment = env,
                        BuildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUri_Legacy),
                        TfsUri = Environment.GetEnvironmentVariable(EnvironmentVariables.TfsCollectionUri_Legacy),
                        BuildDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildDirectory_Legacy)
                    };

                    break;

                case BuildEnvironment.TeamBuild:
                    logger.LogMessage(Resources.SETTINGS_InTeamBuild);
                    settings = new TeamBuildSettings()
                    {
                        BuildEnvironment = env,
                        BuildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUri_TFS2015),
                        TfsUri = Environment.GetEnvironmentVariable(EnvironmentVariables.TfsCollectionUri_TFS2015),
                        BuildDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildDirectory_TFS2015)
                    };
                    
                    break;

                default:
                    logger.LogMessage(Resources.SETTINGS_NotInTeamBuild);

                    string buildDir = Environment.GetEnvironmentVariable(EnvironmentVariables.SQAnalysisRootPath);
                    if (string.IsNullOrEmpty(buildDir))
                    {
                        buildDir = Path.GetTempPath();
                    }

                    settings = new TeamBuildSettings()
                    {
                        BuildEnvironment = env,
                        BuildDirectory = buildDir
                    };

                    break;
            }

            return settings;
        }

        /// <summary>
        /// Returns the type of the current build enviroment: not under TeamBuild, legacy TeamBuild, "new" TeamBuild
        /// </summary>
        public static BuildEnvironment GetBuildEnvironemnt()
        {
            BuildEnvironment env = BuildEnvironment.NotTeamBuild;

            if (IsInTeamBuild)
            {
                // Work out which flavour of TeamBuild
                string buildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUri_Legacy);
                if (string.IsNullOrEmpty(buildUri))
                {
                    buildUri = Environment.GetEnvironmentVariable(EnvironmentVariables.BuildUri_TFS2015);
                    if (!string.IsNullOrEmpty(buildUri))
                    {
                        env = BuildEnvironment.TeamBuild;
                    }
                }
                else
                {
                    env = BuildEnvironment.LegacyTeamBuild;
                }
            }
            return env;
        }

        public static bool IsInTeamBuild
        {
            get
            {                
                return TryGetBoolEnvironmentVariable(EnvironmentVariables.IsInTeamBuild, false);
            }
        }

        public static bool SkipLegacyCodeCoverageProcessing
        {
            get
            {
                return TryGetBoolEnvironmentVariable(EnvironmentVariables.SkipLegacyCodeCoverage, false);
            }
        }

        public static int LegacyCodeCoverageProcessingTimeout
        {
            get
            {
                return TryGetIntEnvironmentVariable(EnvironmentVariables.LegacyCodeCoverageTimeoutInMs, DefaultLegacyCodeCoverageTimeout);
            }
        }

        #endregion

        #region Public properties

        public BuildEnvironment BuildEnvironment
        {
            get;
            private set;
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
                return Path.Combine(this.BuildDirectory, "SQTemp", "Config");
            }
        }

        public string SonarOutputDir
        {
            get
            {
                return Path.Combine(this.BuildDirectory, "SQTemp", "Output");
            }
        }

        public string AnalysisConfigFilePath
        {
            get { return Path.Combine(this.SonarConfigDir, ConfigFileName); }
        }

        #endregion

        #region Private methods

        private static bool TryGetBoolEnvironmentVariable(string envVar, bool defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(envVar);

            bool result;
            if (value != null && bool.TryParse(value, out result))
            {
                return result;
            }
            return defaultValue;
        }

        private static int TryGetIntEnvironmentVariable(string envVar, int defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(envVar);

            int result;
            if (value != null &&
                int.TryParse(value,
                    System.Globalization.NumberStyles.Integer, // don't allow hex, real etc
                    System.Globalization.CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return defaultValue;
        }

        #endregion

    }
}

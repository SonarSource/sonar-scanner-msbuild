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

        internal static class TeamBuildEnvironmentVariables
        {
            /// <summary>
            /// Environment variable that specifies the directory to use as the 
            /// analysis root in non-TFS cases
            /// </summary>
            public const string SQAnalysisRootPath = "SQ_BUILDDIRECTORY";

            public const string IsInTeamBuild = "TF_Build";
            public const string TfsCollectionUri_Legacy = "TF_BUILD_COLLECTIONURI";
            public const string BuildUri_Legacy = "TF_BUILD_BUILDURI";
            public const string BuildDirectory_Legacy = "TF_BUILD_BUILDDIRECTORY";
            //        public const string BinariesDirectory = "TF_BUILD_BINARIESDIRECTORY";

            // Other available environment variables:
            //TF_BUILD_BUILDDEFINITIONNAME: SimpleBuild1
            //TF_BUILD_BUILDNUMBER: SimpleBuild1_20150310.4
            //TF_BUILD_BUILDREASON: Manual
            //TF_BUILD_DROPLOCATION: 
            //TF_BUILD_SOURCEGETVERSION: C14
            //TF_BUILD_SOURCESDIRECTORY: C:\Builds\1\Demos\SimpleBuild1\src
            //TF_BUILD_TESTRESULTSDIRECTORY: C:\Builds\1\Demos\SimpleBuild1\tst

            // TFS 2015 Environment variables
            public const string TfsCollectionUri_TFS2015 = "SYSTEM_TEAMFOUNDATIONCOLLECTIONURI";
            public const string BuildUri_TFS2015 = "BUILD_BUILDURI";
            public const string BuildDirectory_TFS2015 = "AGENT_BUILDDIRECTORY";
        }

        #region Public methods

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
                        BuildUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildUri_Legacy),
                        TfsUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.TfsCollectionUri_Legacy),
                        BuildDirectory = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildDirectory_Legacy)
                    };

                    break;

                case BuildEnvironment.TeamBuild:
                    logger.LogMessage(Resources.SETTINGS_InTeamBuild);
                    settings = new TeamBuildSettings()
                    {
                        BuildEnvironment = env,
                        BuildUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildUri_TFS2015),
                        TfsUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.TfsCollectionUri_TFS2015),
                        BuildDirectory = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildDirectory_TFS2015)
                    };
                    
                    break;

                default:
                    logger.LogMessage(Resources.SETTINGS_NotInTeamBuild);

                    string buildDir = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.SQAnalysisRootPath);
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

        public static BuildEnvironment GetBuildEnvironemnt()
        {
            BuildEnvironment env = BuildEnvironment.NotTeamBuild;

            if (IsInTeamBuild)
            {
                // Work out which flavour of TeamBuild
                string buildUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildUri_Legacy);
                if (string.IsNullOrEmpty(buildUri))
                {
                    buildUri = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.BuildUri_TFS2015);
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
                string value = Environment.GetEnvironmentVariable(TeamBuildEnvironmentVariables.IsInTeamBuild);
                
                bool result;
                if (value != null && bool.TryParse(value, out result))
                {
                    return result;
                }
                return false;
            }
        }

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
        
    }
}

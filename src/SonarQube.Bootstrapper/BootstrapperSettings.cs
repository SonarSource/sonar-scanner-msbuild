//-----------------------------------------------------------------------
// <copyright file="BootstrapperSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Bootstrapper.Properties;
using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;

namespace SonarQube.Bootstrapper
{
    public class BootstrapperSettings : IBootstrapperSettings
    {
        // Note: these constant values must be kept in step with the values used 
        // in the SonarQube.TeamBuild.Integration assembly, and also the targets files.
        public const string SQAnalysisRootPath = "SQ_BUILDDIRECTORY";
        public const string BuildDirectory_Legacy = "TF_BUILD_BUILDDIRECTORY";
        public const string BuildDirectory_TFS2015 = "AGENT_BUILDDIRECTORY";

        /// <summary>
        /// The list of environment variables that should be checked in order to find the
        /// root folder under which all analysis ouput will be written
        /// </summary>
        private static readonly string[] DirectoryEnvVarNames = {
                SQAnalysisRootPath,        // Variable to use in non-TFS cases, or to override the TFS settings
                BuildDirectory_Legacy,  // Legacy TeamBuild directory (TFS2013 and earlier)
                BuildDirectory_TFS2015      // TeamBuild 2015 and later build directory
               };

        private const string RelativePathToDownloadDir = @"SQTemp\bin";


        private Settings appConfig;

        private ILogger logger;

        private string sonarQubeUrl;
        private string downloadDir;
        private string preProcFilePath;
        private string postProcFilePath;

        #region Constructor(s)

        public BootstrapperSettings(ILogger logger) : this(logger, Settings.Default)
        {
        }

        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        internal BootstrapperSettings(ILogger logger, Properties.Settings appConfigSettings)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (appConfigSettings == null)
            {
                throw new ArgumentNullException("appConfigSettings");
            }

            this.logger = logger;
            this.appConfig = appConfigSettings;
        }

        #endregion

        #region IBootstrapperSettings

        public string SonarQubeUrl
        {
            get
            {
                if (this.sonarQubeUrl == null)
                {
                    // Use the value specified in the settings file first...
                    string url = this.appConfig.SonarQubeUrl;
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        // ...otherwise look for the value in the runner-properties file
                        url = GetUrlFromPropertiesFile();
                    }
                    this.sonarQubeUrl = url;
                }

                return this.sonarQubeUrl;
            }
        }

        public string DownloadDirectory
        {
            get
            {
                if (this.downloadDir == null)
                {
                    this.downloadDir = CalculateDownloadDir(this.appConfig, this.logger);
                }
                return this.downloadDir;
            }
        }

        public string PreProcessorFilePath
        {
            get
            {
                if (this.preProcFilePath == null)
                {
                    Debug.Assert(this.appConfig.PreProcessExe != null, "Not expecting the PreProcessExe setting to be null - it should have a default value");
                    this.preProcFilePath = this.EnsurePathIsAbsolute(this.appConfig.PreProcessExe);
                }
                return this.preProcFilePath;
            }
        }

        public string PostProcessorFilePath
        {
            get
            {
                if (this.postProcFilePath == null)
                {
                    Debug.Assert(this.appConfig.PostProcessExe != null, "Not expecting the PostProcessExe setting to be null - it should have a default value");
                    this.postProcFilePath = this.EnsurePathIsAbsolute(this.appConfig.PostProcessExe);
                }                
                return this.postProcFilePath;
            }
        }

        public int PreProcessorTimeoutInMs
        {
            get
            {
                return this.appConfig.PreProcessorTimeoutInMs;
            }
        }

        public int PostProcessorTimeoutInMs
        {
            get
            {
                return this.appConfig.PostProcessorTimeoutInMs;
            }
        }

        #endregion

        #region Private methods

        private static string CalculateDownloadDir(Settings settings, ILogger logger)
        {
            // Use the app setting if supplied...
            string dir = settings.DownloadDir;
            if (string.IsNullOrWhiteSpace(dir))
            {
                // ... otherwise work it out from the environment variables
                logger.LogMessage(Resources.INFO_UsingEnvVarToGetDirectory);
                string rootDir = GetFirstEnvironmentVariable(DirectoryEnvVarNames, logger);

                if (!string.IsNullOrWhiteSpace(rootDir))
                {
                    dir = Path.Combine(rootDir, RelativePathToDownloadDir);
                }
            }

            return dir;
        }

        /// <summary>
        /// Returns the value of the first environment variable from the supplied
        /// list that return a non-empty value
        /// </summary>
        private static string GetFirstEnvironmentVariable(string[] varNames, ILogger logger)
        {
            string result = null;
            foreach (string varName in varNames)
            {
                string value = Environment.GetEnvironmentVariable(varName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    logger.LogMessage(Resources.INFO_UsingBuildEnvironmentVariable, varName, value);
                    result = value;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Gets the URL from the sonar-runner.properties file. Throws if the
        /// properties file cannot be located.
        /// </summary>
        private static string GetUrlFromPropertiesFile()
        {
            var sonarRunnerProperties = FileLocator.FindDefaultSonarRunnerProperties();
            if (sonarRunnerProperties == null)
            {
                throw new ArgumentException(Resources.ERROR_CouldNotFindSonarRunnerProperties);
            }

            var propertiesProvider = new FilePropertiesProvider(sonarRunnerProperties);
            var server = propertiesProvider.GetProperty(SonarProperties.HostUrl);

            Debug.Assert(!string.IsNullOrWhiteSpace(server), "Not expecting the host url property in the sonar-runner.properties file to be null/empty");

            return server;
        }

        private string EnsurePathIsAbsolute(string file)
        {
            string absPath;

            // If the path isn't rooted then assume it is in the download dir
            if (Path.IsPathRooted(file))
            {
                absPath = file;
            }
            else
            {
                absPath = Path.Combine(this.DownloadDirectory, file);
                absPath = Path.GetFullPath(absPath); // strip out relative elements
            }
            return absPath;
        }

        #endregion
    }
}

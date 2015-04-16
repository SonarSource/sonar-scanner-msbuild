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
    class BootstrapperSettings : IBootstrapperSettings
    {
        private const string RelativePathToDownloadDir = @"SQTemp\bin";

        /// <summary>
        /// The list of environment variables that should be checked in order to find the
        /// root folder under which all analysis ouput will be written
        /// </summary>
        private static readonly string[] DirectoryEnvVarNames = {
                "SQ_BUILDDIRECTORY",        // Variable to use in non-TFS cases, or to override the TFS settings
                "TF_BUILD_BUILDDIRECTORY",  // Legacy TeamBuild directory (TFS2013 and earlier)
                "AGENT_BUILDDIRECTORY"      // TeamBuild 2015 and later build directory
               };

        private Settings settings;

        private ILogger logger;

        private string sonarQubeUrl;
        private string downloadDir;
        private string preProcFilePath;
        private string postProcFilePath;

        #region Public methods

        public BootstrapperSettings(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.logger = logger;
            this.settings = Settings.Default;       
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
                    string url = this.settings.SonarQubeUrl;
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
                    this.downloadDir = CalculateDownloadDir(this.settings, this.logger);
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
                    string file = this.settings.PreProcessExe;
                    Debug.Assert(file != null, "Not expecting the PreProcessExe setting to be null - it should have a default value");

                    // If the path isn't rooted then assume it is in the download dir
                    if (!Path.IsPathRooted(file))
                    {
                        file = Path.Combine(this.DownloadDirectory, file);
                    }
                    this.preProcFilePath = file;
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
                    string file = this.settings.PostProcessExe;
                    Debug.Assert(file != null, "Not expecting the PostProcessExe setting to be null - it should have a default value");

                    // If the path isn't rooted then assume it is in the download dir
                    if (!Path.IsPathRooted(file))
                    {
                        file = Path.Combine(this.DownloadDirectory, file);
                    }
                    this.postProcFilePath = file;
                }
                
                return this.postProcFilePath;
            }
        }

        public int PreProcessorTimeoutInMs
        {
            get
            {
                return this.settings.PreProcessorTimeoutInMs;
            }
        }

        public int PostProcessorTimeoutInMs
        {
            get
            {
                return this.settings.PostProcessorTimeoutInMs;
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
                logger.LogMessage("Using environment variables to determine the download directory...");
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
                    logger.LogMessage("Using environment variable '{0}', value '{1}'", varName, value);
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

        #endregion
    }
}

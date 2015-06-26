//-----------------------------------------------------------------------
// <copyright file="BootstrapperSettings.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using System;
using System.Diagnostics;
using System.IO;

namespace SonarQube.Bootstrapper
{
    public class BootstrapperSettings : IBootstrapperSettings
    {
        // Note: these constant values must be kept in sync with the targets files.
        public const string BuildDirectory_Legacy = "TF_BUILD_BUILDDIRECTORY";
        public const string BuildDirectory_TFS2015 = "AGENT_BUILDDIRECTORY";

        public const string PreProcessorExeName = "SonarQube.MSBuild.PreProcessor.exe";
        public const string PostProcessorExeName = "SonarQube.MSBuild.PostProcessor.exe";

        /// <summary>
        /// The logical version of the bootstrapper API that the pre/post-processor must support
        /// </summary>
        /// <remarks>The version string should have at least a major and a minor component</remarks>
        private const string BootstrapperLogicalVersionString = "1.0";

        /// <summary>
        /// The file name where the supported bootstrapper logical API versions are mentioned
        /// </summary>
        private const string SupportedBootstrapperVersionsFilename = "SupportedBootstrapperVersions.xml";

        /// <summary>
        /// The list of environment variables that should be checked in order to find the
        /// root folder under which all analysis ouput will be written
        /// </summary>
        private static readonly string[] DirectoryEnvVarNames = {
                BuildDirectory_Legacy,      // Legacy TeamBuild directory (TFS2013 and earlier)
                BuildDirectory_TFS2015      // TeamBuild 2015 and later build directory
               };

        public const string RelativePathToTempDir = @".sonarqube";
        public const string RelativePathToDownloadDir = @"bin";

        private readonly ILogger logger;

        private readonly string sonarQubeUrl;
        private string tempDir;
        private string preProcFilePath;
        private string postProcFilePath;

        #region Constructor(s)
        
        public BootstrapperSettings(string sonarQubeUrl, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(sonarQubeUrl))
            {
                throw new ArgumentNullException("sonarQubeUrl");
            }

            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.sonarQubeUrl = sonarQubeUrl;
            this.logger = logger;
        }

        #endregion

        #region IBootstrapperSettings

        public string SonarQubeUrl {  get { return this.sonarQubeUrl; } }

        public string TempDirectory
        {
            get
            {
                if (this.tempDir == null)
                {
                    this.tempDir = CalculateTempDir(this.logger);
                }
                return this.tempDir;
            }
        }

        public string DownloadDirectory
        {
            get
            {
                return Path.Combine(TempDirectory, RelativePathToDownloadDir);
            }
        }

        public string PreProcessorFilePath
        {
            get
            {
                if (this.preProcFilePath == null)
                {
                    this.preProcFilePath = this.EnsurePathIsAbsolute(PreProcessorExeName);
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
                    this.postProcFilePath = this.EnsurePathIsAbsolute(PostProcessorExeName);
                }                
                return this.postProcFilePath;
            }
        }

        public string SupportedBootstrapperVersionsFilePath
        {
            get
            {
                return this.EnsurePathIsAbsolute(SupportedBootstrapperVersionsFilename);
            }
        }

        public Version BootstrapperVersion
        {
            get
            {
                return new Version(BootstrapperLogicalVersionString);
            }
        }

        #endregion

        #region Private methods

        private static string CalculateTempDir(ILogger logger)
        {
            logger.LogMessage(Resources.INFO_UsingEnvVarToGetDirectory);
            string rootDir = GetFirstEnvironmentVariable(DirectoryEnvVarNames, logger);

            if (string.IsNullOrWhiteSpace(rootDir))
            {
                rootDir = Directory.GetCurrentDirectory();
            }

            return Path.Combine(rootDir, RelativePathToTempDir);
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

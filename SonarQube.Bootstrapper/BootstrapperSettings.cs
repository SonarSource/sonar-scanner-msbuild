/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2017 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */
 
using SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace SonarQube.Bootstrapper
{
    public class BootstrapperSettings : IBootstrapperSettings
    {
        #region Working directory

        // Variables used when calculating the working directory

        // Note: these constant values must be kept in sync with the targets files.
        public const string BuildDirectory_Legacy = "TF_BUILD_BUILDDIRECTORY";

        public const string BuildDirectory_TFS2015 = "AGENT_BUILDDIRECTORY";

        /// <summary>
        /// The list of environment variables that should be checked in order to find the
        /// root folder under which all analysis output will be written
        /// </summary>
        private static readonly string[] DirectoryEnvVarNames = {
                BuildDirectory_Legacy,      // Legacy TeamBuild directory (TFS2013 and earlier)
                BuildDirectory_TFS2015      // TeamBuild 2015 and later build directory
               };

        public const string RelativePathToTempDir = @".sonarqube";
        public const string RelativePathToDownloadDir = @"bin";

        #endregion Working directory

        private readonly ILogger logger;

        private readonly AnalysisPhase analysisPhase;
        private readonly IEnumerable<string> childCmdLineArgs;
        private readonly LoggerVerbosity verbosity;

        private string tempDir;

        #region Constructor(s)

        public BootstrapperSettings(AnalysisPhase phase, IEnumerable<string> childCmdLineArgs, LoggerVerbosity verbosity, ILogger logger)
        {
            this.analysisPhase = phase;
            this.childCmdLineArgs = childCmdLineArgs;
            this.verbosity = verbosity;
            this.logger = logger ?? throw new ArgumentNullException("logger");
        }

        #endregion Constructor(s)

        #region IBootstrapperSettings

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

        public AnalysisPhase Phase
        {
            get { return this.analysisPhase; }
        }

        public IEnumerable<string> ChildCmdLineArgs
        {
            get { return this.childCmdLineArgs; }
        }

        public LoggerVerbosity LoggingVerbosity
        {
            get { return this.verbosity; }
        }

        public string ScannerBinaryDirPath
        {
            get { return Path.GetDirectoryName(typeof(BootstrapperSettings).Assembly.Location); }
        }

        #endregion IBootstrapperSettings

        #region Private methods

        private static string CalculateTempDir(ILogger logger)
        {
            logger.LogDebug(Resources.MSG_UsingEnvVarToGetDirectory);
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
                    logger.LogDebug(Resources.MSG_UsingBuildEnvironmentVariable, varName, value);
                    result = value;
                    break;
                }
            }
            return result;
        }

        #endregion Private methods
    }
}
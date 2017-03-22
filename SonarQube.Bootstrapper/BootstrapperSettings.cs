/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2015-2017 SonarSource SA and Microsoft Corporation
 * mailto: contact AT sonarsource DOT com
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
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
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.analysisPhase = phase;
            this.childCmdLineArgs = childCmdLineArgs;
            this.verbosity = verbosity;
            this.logger = logger;
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
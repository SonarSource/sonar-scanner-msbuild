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
using SonarQube.TeamBuild.Integration.Interfaces;
using System;
using System.Diagnostics;
using System.IO;

namespace SonarQube.TeamBuild.Integration
{
    public abstract class CoverageReportProcessorBase : ICoverageReportProcessor
    {
        private const string XmlReportFileExtension = "coveragexml";
        private readonly ICoverageReportConverter converter;

        private AnalysisConfig config;
        private ITeamBuildSettings settings;
        private ILogger logger;

        private bool succesfullyInitialised = false;

        protected CoverageReportProcessorBase(ICoverageReportConverter converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            this.converter = converter;
        }

        #region ICoverageReportProcessor interface


        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.config = config;
            this.settings = settings;
            this.logger = logger;

            this.succesfullyInitialised =  this.converter.Initialize(logger);
            return succesfullyInitialised;
        }

        public bool ProcessCoverageReports()
        {
            if (!this.succesfullyInitialised)
            {
                throw new InvalidOperationException(Resources.EX_CoverageReportProcessorNotInitialised);
            }

            Debug.Assert(this.config != null, "Expecting the config to not be null. Did you call Initialise() ?");

            // Fetch all of the report URLs
            this.logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

            string binaryFilePath;
            bool success = this.TryGetBinaryReportFile(this.config, this.settings, this.logger, out binaryFilePath);

            if (success && binaryFilePath != null)
            {
                success = this.ProcessBinaryCodeCoverageReport(binaryFilePath);
            }

            return success;
        }

        protected abstract bool TryGetBinaryReportFile(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger, out string binaryFilePath);

        #endregion

        #region Private methods

        private bool ProcessBinaryCodeCoverageReport(string binaryCoverageFilePath)
        {
            bool success;
            string xmlFileName = Path.ChangeExtension(binaryCoverageFilePath, XmlReportFileExtension);

            Debug.Assert(!File.Exists(xmlFileName), "Not expecting a file with the name of the binary-to-XML conversion output to already exist: " + xmlFileName);
            success = converter.ConvertToXml(binaryCoverageFilePath, xmlFileName, this.logger);

            if (success)
            {
                logger.LogDebug(Resources.PROC_DIAG_UpdatingProjectInfoFiles);
                InsertCoverageAnalysisResults(this.config.SonarOutputDir, xmlFileName);
            }

            return success;
        }

        /// <summary>
        /// Insert code coverage results information into each projectinfo file
        /// </summary>
        private static void InsertCoverageAnalysisResults(string sonarOutputDir, string coverageFilePath)
        {
            foreach (string projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    ProjectInfo projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = coverageFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }


        #endregion

    }
}

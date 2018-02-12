/*
 * SonarQube Scanner for MSBuild
 * Copyright (C) 2016-2018 SonarSource SA
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

using System;
using System.Diagnostics;
using System.IO;
using SonarQube.Common;
using SonarQube.TeamBuild.Integration.Interfaces;

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
            this.converter = converter ?? throw new ArgumentNullException("converter");
        }

        #region ICoverageReportProcessor interface

        public bool Initialise(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger)
        {
            this.config = config ?? throw new ArgumentNullException("config");
            this.settings = settings ?? throw new ArgumentNullException("settings");
            this.logger = logger ?? throw new ArgumentNullException("logger");

            succesfullyInitialised = converter.Initialize(logger);
            return succesfullyInitialised;
        }

        public bool ProcessCoverageReports()
        {
            if (!succesfullyInitialised)
            {
                throw new InvalidOperationException(Resources.EX_CoverageReportProcessorNotInitialised);
            }

            Debug.Assert(config != null, "Expecting the config to not be null. Did you call Initialise() ?");

            // Fetch all of the report URLs
            logger.LogInfo(Resources.PROC_DIAG_FetchingCoverageReportInfoFromServer);

            var success = TryGetBinaryReportFile(config, settings, logger, out string binaryFilePath);

            if (success && binaryFilePath != null)
            {
                success = ProcessBinaryCodeCoverageReport(binaryFilePath);
            }

            if (TryGetTrxFile(config, settings, logger, out var trxFilePath))
            {
                InsertTestAnalysisResults(config.SonarOutputDir, trxFilePath);
            }

            return success;
        }

        protected abstract bool TryGetBinaryReportFile(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger, out string binaryFilePath);

        protected abstract bool TryGetTrxFile(AnalysisConfig config, ITeamBuildSettings settings, ILogger logger, out string trxFilePath);

        #endregion ICoverageReportProcessor interface

        #region Private methods

        private bool ProcessBinaryCodeCoverageReport(string binaryCoverageFilePath)
        {
            bool success;
            var xmlFileName = Path.ChangeExtension(binaryCoverageFilePath, XmlReportFileExtension);

            Debug.Assert(!File.Exists(xmlFileName), "Not expecting a file with the name of the binary-to-XML conversion output to already exist: " + xmlFileName);
            success = converter.ConvertToXml(binaryCoverageFilePath, xmlFileName, logger);

            if (success)
            {
                logger.LogDebug(Resources.PROC_DIAG_UpdatingProjectInfoFiles);
                InsertCoverageAnalysisResults(config.SonarOutputDir, xmlFileName);
            }

            return success;
        }

        /// <summary>
        /// Insert code coverage results information into each projectinfo file
        /// </summary>
        private static void InsertCoverageAnalysisResults(string sonarOutputDir, string coverageFilePath)
        {
            foreach (var projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                var projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    var projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = coverageFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }

        /// <summary>
        /// Insert code coverage results information into each projectinfo file
        /// </summary>
        private static void InsertTestAnalysisResults(string sonarOutputDir, string testResultFilePath)
        {
            foreach (var projectFolderPath in Directory.GetDirectories(sonarOutputDir))
            {
                var projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    var projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.TestResults.ToString(), Location = testResultFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }

        #endregion Private methods
    }
}

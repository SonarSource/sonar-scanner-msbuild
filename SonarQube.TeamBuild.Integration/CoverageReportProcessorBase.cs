//-----------------------------------------------------------------------
// <copyright file="CoverageReportProcessorBase.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
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
        private TeamBuildSettings settings;
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


        public bool Initialise(AnalysisConfig config, TeamBuildSettings settings, ILogger logger)
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

        protected abstract bool TryGetBinaryReportFile(AnalysisConfig config, TeamBuildSettings settings, ILogger logger, out string binaryFilePath);

        #endregion

        #region Private methods

        private bool ProcessBinaryCodeCoverageReport(string binaryCoverageFilePath)
        {
            bool success = false;
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
                ProjectInfo projectInfo = null;

                string projectInfoPath = Path.Combine(projectFolderPath, FileConstants.ProjectInfoFileName);

                if (File.Exists(projectInfoPath))
                {
                    projectInfo = ProjectInfo.Load(projectInfoPath);
                    projectInfo.AnalysisResults.Add(new AnalysisResult() { Id = AnalysisType.VisualStudioCodeCoverage.ToString(), Location = coverageFilePath });
                    projectInfo.Save(projectInfoPath);
                }
            }
        }


        #endregion

    }
}
